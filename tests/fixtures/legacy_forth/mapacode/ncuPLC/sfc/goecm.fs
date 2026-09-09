-work

\ ==================== Go ECM ====================
\ 說明:
\   1. 監聽 cutting? 命令回傳值, 處理 v 軸振動/停止，電解液給水/停水及 PEM 電源放電/關電之工作
\   2. 處理不同 feed-mode 下對應的加工進給策略
\   3. 開始加工時(cutting? = false>true)根據 PCC004 參數的設定值，提前啟動電解液給水以排除管子內的空氣
\   4. 加工完成時(cutting? = true>false)根據 PCC005 參數的設定值，延遲關閉電解液給水以沖洗工件
\   5. 外部可以開/關 ECM-going 旗標，命令 Go ECM 工作/休眠
\   6. 外部可以藉由 GoECM-fault 旗標判斷是否有發生放電加工異警
\   7. 外部可以藉由 GoECM-in-idle 旗標判斷 Go ECM 是否已回到 idle 狀態
\ 
\ SFC：
\              GoECM-init
\                   |
\                   + GoECM-init-done?
\                   |
\                   v
\              GoECM-idle
\                   |
\                   + GoECM-start?
\                   |
\                   v
\           GoECM-start-forth
\                   |
\                   +-----------------------+
\                   |                       |
\                   + GoECM-start-done?     + GoECM-start-abort?
\                   |                       |
\                   v                       v
\             GoECM-serving         GoECM-abort-forth
\                   |
\                   +-----------------------+
\                   |                       |
\                   + GoECM-end?            + GoECM-abort?
\                   |                       |
\                   v                       v
\            GoECM-end-forth        GoECM-abort-forth
\                   |
\                   +-----------------------+
\                   |                       |
\                   + GoECM-end-done?       + GoECM-end-abort?
\                   |                       |
\                   v                       v
\              GoECM-init           GoECM-abort-forth
\                                           |
\                                           + GoECM-abort-done?
\                                           |
\                                           v
\                                      GoECM-init
\

variable GoECM-init-done
variable GoECM-start-step
variable GoECM-end-step
variable GoECM-abort-step
variable GoECM-short-buzzer single-flash-beep GoECM-short-buzzer !
variable GoECM-short-to-stop-buzzer single-solid-beep GoECM-short-to-stop-buzzer !

: feed-ratio++ ( -- )
    feed-ratio f@ feed-ratio-delta f@ f+
    1e fmin
    feed-ratio f!
;

: feed-ratio-- ( -- )
    feed-ratio f@ feed-ratio-delta f@ f-
    feed-ratio-min f@ fmax
    feed-ratio f!
;



\ ==================== steps ====================
\ Go ECM 初始化
: GoECM-init ( -- )
    1 GoECM-start-step !            \ step 從 1 開始，0 代表流程完成
    1 GoECM-end-step !
    1 GoECM-abort-step !
    feed-short off
    1e feed-ratio f!
    amp-ready off
    GoECM-fault off
    GoECM-V-fault off
    GoECM-in-idle on

    GoECM-init-done on
;
step GoECM-init

\ 閒置中
: GoECM-idle ( -- )
    ( do nothing )
;
step GoECM-idle

\ 開始程序
: GoECM-start-forth ( -- )
    1 group! 0e vcmd!                       \ 在開始程序中都強制 vcmd 為 0

    GoECM-start-step @
    case
        \ 關閉量測電路
        1 of
            GoECM-in-idle off                       \ 關閉 GoECM-in-idle 旗標
            measure-go off                          \ 關閉量測電路
            measure-on/off-delay 0timer             \ 更新 timer
            1 GoECM-start-step +!                   \ 切換至下一步
        endof

        \ 等待量測電路關閉
        2 of
            measure-off?                            \ 若量測電路已關閉
            measure-on/off-delay timer-expired? and \ TODO:因目前 measure on/off 不等待量測電路回傳狀態，安全起見等待一段延遲後才繼續
            if
                1 GoECM-start-step +!                   \ 切換至下一步
            then
        endof

        \ 啟動 aqua
        3 of
            +aqua-run                               \ 啟動 aqua
            aqua-pre-start 0timer                   \ 更新 timer
            1 GoECM-start-step +!                   \ 切換至下一步
        endof

        \ aqua 預啟動等待
        4 of
            aqua-pre-start timer-expired?
            if
                1 GoECM-start-step +!                   \ 切換至下一步
            then
        endof

        \ 開始振動與放電準備
        5 of
            2 group! +group gstart                  \ group2 開始振動
            1 discharge-on!                         \ 設定 discharge-on-prepare = 1
            1 GoECM-start-step +!                   \ 切換至下一步
        endof

        \ 確定振動軸有到位
        6 of
            amp-ready @                             \ 若 amp-ready = true 表示有振到需求位置
            if
                1 GoECM-start-step +!                   \ 切換至下一步
            then
        endof

        \ 等待放電準備完成
        7 of
            discharge-on-ready@ 0<>                 \ 若 discharge-on-ready = 1
            if
                1 GoECM-start-step +!                   \ 切換至下一步
            then
        endof

        \ 送出開始放電命令
        8 of
            Go-ECM-ch-slv +dout                     \ 開啟 Go_ECM dout
            1 GoECM-start-step +!                   \ 切換至下一步
        endof

        \ 等待開始放電
        9 of
            ECM-done-ch-slv ec-din@                 \ 若 ECM_done din = true
            if
                1 GoECM-start-step +!                   \ 切換至下一步
            then
        endof

        \ 開始加工
        10 of
            1 group! apk-spd@ feed-ratio f@ f* mm/min vcmd! \ 設定 vcmd
            +chiller                                \ 開啟 chiller
            feed-transmission-period 0timer         \ 更新 timer
            0 GoECM-start-step !                    \ start 程序完成
        endof
    endcase
;
step GoECM-start-forth

\ serving, 檢查異常與處理加工進給策略
: GoECM-serving ( -- )
    ECM-done-ch-slv ec-din@ not                     \ ECM-done din = false
    discharge-on-ready@ 0= or                       \ 或 discharge-on-ready = 0
    is-PROT? or
    is-OVP? or
    is-TmpWarn? or
    is-NegVol? or
    is-CircuitFail? or
    aqua-ems@ not or
    aqua-ready@ not or
    if
        GoECM-fault on                              \ 開啟 Go-ECM-fault 旗標
    else
        feed-mode @
        case
            safe-mode of
                cutting-short?                      \ 若發生加工中短路
                if
                    GoECM-fault on
                    GoECM-short-to-stop-buzzer set-buzzer
                    ." error|Short alarm detected in safe feed mode.;A1541" cr
                then
            endof
            
            normal-mode of
                cutting-short?                      \ 若發生加工中短路
                if
                    feed-short on
                then

                feed-transmission-period timer-expired?
                if
                    feed-short @
                    if
                        feed-ratio f@ feed-ratio-min f@ f- 0.0001e f<
                        if
                            GoECM-fault on
                            GoECM-short-to-stop-buzzer set-buzzer
                            ." error|Feed ratio lower limit reached in short normal feed mode.;A1542" cr
                        else
                            feed-ratio--
                            GoECM-short-buzzer set-buzzer
                            ." log|Short alarm detected. feed ratio decreased:" feed-ratio f@ f. cr
                            1 group! apk-spd@ feed-ratio f@ f* mm/min vcmd!
                        then
                    else
                        feed-ratio f@ 1e f<
                        if
                            feed-ratio++
                            ." log|No short alarm detected. feed ratio increased:" feed-ratio f@ f. cr
                            1 group! apk-spd@ feed-ratio f@ f* mm/min vcmd!
                        then
                    then
                    feed-short off
                    feed-transmission-period 0timer
                then
            endof
            
            ignore-mode of
                ( do nothing )
            endof
        endcase

        arc-feed-mode @
        case
            safe-mode of
                cutting-arc?                     \ 若發生加工中arc
                if
                    GoECM-fault on
                    GoECM-short-to-stop-buzzer set-buzzer
                    ." error|Arc alarm detected in safe feed mode.;A1543" cr
                then
            endof
            
            normal-mode of
                cutting-arc?                      \ 若發生加工中arc
                if
                    feed-short on
                then

                feed-transmission-period timer-expired?
                if
                    feed-short @
                    if
                        feed-ratio f@ feed-ratio-min f@ f- 0.0001e f<
                        if
                            GoECM-fault on
                            GoECM-short-to-stop-buzzer set-buzzer
                            ." error|Feed ratio lower limit reached in arc normal feed mode.;A1544" cr
                        else
                            feed-ratio--
                            GoECM-short-buzzer set-buzzer
                            ." log|Arc alarm detected. feed ratio decreased:" feed-ratio f@ f. cr
                            1 group! apk-spd@ feed-ratio f@ f* mm/min vcmd!
                        then
                    else
                        feed-ratio f@ 1e f<
                        if
                            feed-ratio++
                            ." log|No arc alarm detected. feed ratio increased:" feed-ratio f@ f. cr
                            1 group! apk-spd@ feed-ratio f@ f* mm/min vcmd!
                        then
                    then
                    feed-short off
                    feed-transmission-period 0timer
                then
            endof
            
            ignore-mode of
                ( do nothing )
            endof
        endcase
    then
;
step GoECM-serving

\ 結束程序，會延遲關閉電解液送水以沖洗工件
: GoECM-end-forth ( -- )
    1 group! 0e vcmd!                       \ 在結束程序中都強制 vcmd 為 0

    GoECM-end-step @
    case
        \ 停止振動與放電
        1 of
            feed-ratio f@ feed-ratio-old f!         \ 紀錄 feed-ratio
            Go-ECM-ch-slv -dout                     \ 關閉 Go_ECM dout
            -chiller                                \ 關閉 chiller
            0 discharge-on!                         \ 設定 discharge-on-prepare 為 0
            2 group! gstop                          \ group2 停止振動
            1 GoECM-end-step +!                     \ 切換至下一步
        endof

        \ 等待振動與放電停止
        2 of
            ECM-done-ch-slv ec-din@ not             \ ECM_done din = false
            discharge-on-ready@ not and             \ 且 discharge-on-ready = 0
            2 group! gstop? and                     \ 且 group2 已停止
            if
                1 GoECM-end-step +!                 \ 切換至下一步
            then
        endof

        \ 關閉 group2，處理 aqua 延遲關閉
        3 of
            2 group! -group                         \ disable group2
            aqua-delay-stop 0timer                  \ 更新 timer
            1 GoECM-end-step +!                     \ 切換至下一步
        endof

        \ 等待 aqua 延遲關閉
        4 of
            aqua-delay-stop timer-expired?
            if
                1 GoECM-end-step +!                     \ 切換至下一步
            then
        endof

        \ 關閉 aqua，開啟量測電路
        5 of
            -aqua-run                               \ 關閉 aqua
            measure-go on                           \ 開啟量測電路
            measure-on/off-delay 0timer             \ 更新 timer
            1 GoECM-end-step +!                     \ 切換至下一步
        endof

        \ 等待量測電路開啟&與aqua確實關閉
        6 of
            touch-ignore @                          \ 忽略碰邊
            measure-on? or                          \ 或量測電路已開啟
            measure-on/off-delay timer-expired? and \ TODO:因目前 measure on/off 不等待量測電路回傳狀態，安全起見等待一段延遲後才繼續
            aqua-state @ not and                    \ 確定aqua已關閉    
            if
                1 GoECM-end-step +!                     \ 切換至下一步
            then
        endof

        \ 停止完成
        7 of
            1 group! rapid-traverse-rate@ vcmd!     \ 設定 vcmd
            0 GoECM-end-step !                      \ stop 程序完成
        endof
    endcase
;
step GoECM-end-forth

\ 中斷程序，不會延遲關閉電解液送水
: GoECM-abort-forth ( -- )
    1 group! 0e vcmd!                       \ 在中斷程序中都強制 vcmd 為 0

    GoECM-abort-step @
    case
        \ 中斷工作
        1 of
            feed-ratio f@ feed-ratio-old f!         \ 紀錄 feed-ratio
            -aqua-run                               \ 關閉 aqua
            Go-ECM-ch-slv -dout                     \ 關閉 Go_ECM dout
            -chiller                                \ 關閉 chiller
            0 discharge-on!                         \ 設定 discharge-on-prepare 為 0
            2 group! gstop                          \ group2 停止振動
            1 GoECM-abort-step +!                   \ 切換至下一步
        endof

        \ 等待
        2 of
            ECM-done-ch-slv ec-din@ not             \ ECM_done din = false
            discharge-on-ready@ not and             \ 且 discharge-on-ready = 0
            2 group! gstop? and                     \ 且 group2 已停止
            aqua-state @ not and                    \ 確定aqua已關閉
            if
                1 GoECM-abort-step +!                   \ 切換至下一步
            then
        endof

        \ 關閉 group2，開啟量測電路
        3 of
            2 group! -group                         \ disable group2
            measure-go on                           \ 開啟量測電路
            measure-on/off-delay 0timer             \ 更新 timer
            1 GoECM-abort-step +!                   \ 切換至下一步
        endof

        \ 等待量測電路開啟
        4 of
            touch-ignore @                          \ 忽略碰邊
            measure-on? or                          \ 或量測電路已開啟
            measure-on/off-delay timer-expired? and \ TODO:因目前 measure on/off 不等待量測電路回傳狀態，安全起見等待一段延遲後才繼續
            if
                1 GoECM-abort-step +!                   \ 切換至下一步
            then
        endof

        \ 中斷完成
        5 of
            1 group! rapid-traverse-rate@ vcmd!     \ 設定 vcmd
            0 GoECM-abort-step !                    \ abort 程序完成
        endof
    endcase
;
step GoECM-abort-forth



\ ==================== transitions ====================
\ 初始化完成?
: GoECM-init-done? ( -- flag )
    GoECM-init-done @
;
transition GoECM-init-done?

\ 是否開始?
: GoECM-start? ( -- flag )
    ECM-going @
    motion-status @ serving = and
    cutting? and
;
transition GoECM-start?

\ 是否中斷 start 流程?
: GoECM-start-abort? ( -- flag )
    ECM-going @ not                 \ 若 start 流程中 ECM-going 被關掉
    motion-status @ serving <> or   \ 或 motion status 離開 serving
;
transition GoECM-start-abort?

\ start 流程是否完成?
: GoECM-start-done? ( -- flag )
    GoECM-start-abort? not
    GoECM-start-step @ 0= and
;
transition GoECM-start-done?

\ 是否中斷加工?
: GoECM-abort? ( -- flag )
    ECM-going @ not                 \ 若加工中 ECM-going 被關掉

    motion-status @ serving <>
    motion-status @ ending <> and
    or                              \ 或 motion status 離開 serving 和 ending

    GoECM-fault @ or                \ 或發生 Go ECM fault
;
transition GoECM-abort?

\ 是否完成加工?
: GoECM-end? ( -- flag )
    motion-status @ ending =        \ 工作結束中
    cutting? not or                 \ 或結束 cutting

    GoECM-abort? not and
;
transition GoECM-end?

\ 是否中斷 end 流程?
: GoECM-end-abort? ( -- flag )
    ECM-going @ not                 \ 若 ECM-going 被關掉

    motion-status @ serving <>
    motion-status @ ending <> and
    or                              \ 或 motion status 離開 serving 和 ending
    
    platform-door@ not or           \ 或 CNC 安全門未緊閉
;
transition GoECM-end-abort?

\ end 流程是否完成?
: GoECM-end-done? ( -- flag )
    GoECM-end-abort? not
    GoECM-end-step @ 0= and
;
transition GoECM-end-done?

\ abort 流程是否完成?
: GoECM-abort-done? ( -- flag )
    GoECM-abort-step @ 0=
;
transition GoECM-abort-done?



\ ==================== links ====================
' GoECM-init        ' GoECM-init-done?   --> ' GoECM-init-done?   ' GoECM-idle        -->
' GoECM-idle        ' GoECM-start?       --> ' GoECM-start?       ' GoECM-start-forth -->

' GoECM-start-forth ' GoECM-start-done?  --> ' GoECM-start-done?  ' GoECM-serving     -->
' GoECM-start-forth ' GoECM-start-abort? --> ' GoECM-start-abort? ' GoECM-abort-forth -->

' GoECM-serving     ' GoECM-end?         --> ' GoECM-end?         ' GoECM-end-forth   -->
' GoECM-serving     ' GoECM-abort?       --> ' GoECM-abort?       ' GoECM-abort-forth -->

' GoECM-end-forth   ' GoECM-end-done?    --> ' GoECM-end-done?    ' GoECM-init        -->
' GoECM-end-forth   ' GoECM-end-abort?   --> ' GoECM-end-abort?   ' GoECM-abort-forth -->

' GoECM-abort-forth ' GoECM-abort-done?  --> ' GoECM-abort-done?  ' GoECM-init        -->

\ ================check-v-postion=================
\ 說明:
\   因為加工中會因為水壓或其他因素導致振動軸不到應該到的位置
\   倘若不到需要的位置, 會因為不到準位, 表示不放電,
\   此時若繼續向下前進加工, 會因為未腐蝕工件, 導致有撞機的疑慮
\   故多設計此sfc, 使其在加工時監控振動軸位置
\   1. 收到ECM-done-ch-slv表示開始加工放電, 則開啟此機制
\   2. delay一小段時間, 使其正式開始加工
\   3. 監控振動軸的位置, 計算所有該放電的點, 與理論值是否接近
\   4. 若持續差異太大則停機

\ SFC：
\              check-v-idle
\                   |
\                   + check-v-go?
\                   |
\                   v
\              check-v-delay
\                   |
\                   + check-v-delay-ok?
\                   |
\                   v
\              check-v-init
\                   |
\                   + check-v-init-done?
\                   |
\                   v
\              check-v-pos
\                   |
\                   + check-v-end?
\                   |
\                   v
\              check-v-idle

variable check-v check-v on \ 開啟才會開啟此功能 才有機會 GoECM-V-fault on 
variable judge-total-point
variable judge-count
variable discharge-point
variable discharge-fail-count

\ 閒置中
: check-v-idle ( -- )
    ( do nothing )
;
step check-v-idle

: check-v-delay ( -- )
    ( do nothing )
;
step check-v-delay

variable check-v-step
variable check-v-init-done
: check-v-init ( -- )
    1 check-v-step !            \ step 從 1 開始，0 代表流程完成
    check-v-init-done on
;
step check-v-init

: check-v-pos ( -- )
    check-v-step @
        case
            1 of
                0 discharge-fail-count !                \ 將放電失敗次數歸0
                GoECM-V-fault off                       \ 重置狀態
                1 check-v-step +!                       \ 切換至下一步
            endof
            2 of
                1000e apk-vf@ 1e fmax f/ 2e f* f>s      \ 由頻率去計算應該檢查多少個點,目前計算2個波形的量
                judge-total-point !                     \ 例如50HZ,則會送1000 / 50 * 2 = 40 個點
                0 judge-count !                         \ 判斷的次數歸0
                0 discharge-point !                     \ 清空放電的點的個數
                1 check-v-step +!                       \ 切換至下一步
            endof
            3 of
                1 judge-count +!                        \ 每判斷一個點就+1
                
                4 axis-real-p@ cur-trigger f@ f- f0< if
                    1 discharge-point +!                \ 倘若該點低於設定準位(表示放電), 則次數+1
                then
                
                judge-count @ judge-total-point @ >= if \ 計算完設定的個數後 step才下一步
                    1 check-v-step +!
                then
            endof
            4 of
                \ 判斷需要放電的點是否小於理論值設定的比例
                \ 若持續小於則計數discharge-fail-count
                \ 持續小於15次, 則判定v軸變形嚴重, 不宜加工              
                working-rate-limit f@ 0.01e apk-vt@ judge-count @ s>f f* f* f* f>s discharge-point @ >  if
                1 discharge-fail-count +!
                else
                    0 discharge-fail-count !
                then
                discharge-fail-count @ 15 > if
                    check-v @ if
                        GoECM-V-fault on
                    then
                then
                5 check-v-step !
            endof
            5 of
                \ ." working_rate|" discharge-point @ s>f 0.01e apk-vt@ judge-count @ s>f f* f* f/ 1e fmin 4 2 f.r cr
                2 check-v-step !
            endof
        endcase
;
step check-v-pos

: check-v-go? ( -- flag )
    ECM-done-ch-slv ec-din@
;
transition check-v-go?

: check-v-delay-ok? ['] check-v-delay elapsed 5000 > ;
transition check-v-delay-ok?

: check-v-init-done? ( -- flag )
    check-v-init-done @
;
transition check-v-init-done?

: check-v-end? ( -- flag )
    ECM-done-ch-slv ec-din@ not
;
transition check-v-end?
\ ==================== links ====================
' check-v-idle          ' check-v-go?           -->
' check-v-go?           ' check-v-delay         -->
' check-v-delay         ' check-v-delay-ok?     -->
' check-v-delay-ok?     ' check-v-init          -->
' check-v-init          ' check-v-init-done?    -->
' check-v-init-done?    ' check-v-pos           -->
' check-v-pos           ' check-v-end?          -->
' check-v-end?          ' check-v-idle          -->

: .check-v
    check-v @
    if
        ." check_v|true" cr
    else
        ." check_v|false" cr
    then
;


marker -work