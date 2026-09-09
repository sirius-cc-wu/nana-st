-work

\ ==================== Go ECM ====================
\ 說明:
\   1. 監聽 cutting? 命令回傳值，電解液給水/停水及 PEM 電源放電/關電之工作
\   2. 處理不同 feed-mode 下對應的加工進給策略
\   3. 開始加工時(cutting? = false>true)根據 PCC004 參數的設定值，提前啟動電解液給水以排除管子內的空氣
\   4. 加工完成時(cutting? = true>false)根據 PCC005 參數的設定值，延遲關閉電解液給水以沖洗工件
\   5. 外部可以開/關 PECM-going 旗標，命令 Go ECM 工作/休眠
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
    GoECM-fault off
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

        \ munk初始化與切算成trigger mode
        3 of
            munk-init
            trigger-mode manual-op-mode!
            munk-cmd-write 
            1 GoECM-start-step +!                   \ 切換至下一步
        endof

        \ 啟動 aqua
        4 of
            +aqua-run                               \ 啟動 aqua
            aqua-pre-start 0timer                   \ 更新 timer
            1 GoECM-start-step +!                   \ 切換至下一步
        endof

        \ aqua 預啟動等待
        5 of
            aqua-pre-start timer-expired?
            if
                1 GoECM-start-step +!                   \ 切換至下一步
            then
        endof

        \ 送出dsp放電波形命令
        6 of
            Go-ECM-dout +dout                     \ 開啟 Go_ECM dout
            1 GoECM-start-step +!                   \ 切換至下一步
        endof

        \ 等待波形命令回饋
        7 of
            ECM-done-ch-slv ec-din@                 \ 若 ECM_done din = true
            if
                1 GoECM-start-step +!                   \ 切換至下一步
            then
        endof

        \ 開始加工
        8 of
            1 group! apk-spd@ feed-ratio f@ f* mm/min vcmd! \ 設定 vcmd
            +switch-on-dc                           \ 放電
            feed-transmission-period 0timer         \ 更新 timer
            0 GoECM-start-step !                    \ start 程序完成
        endof
    endcase
;
step GoECM-start-forth

\ serving, 檢查異常與處理加工進給策略
: GoECM-serving ( -- )
    ECM-done-ch-slv ec-din@ not                     \ ECM-done din = false
    an-error? or                                    \ Munk error
    aqua-ems@ not or
    aqua-ready@ not or
    upper-close? not or
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
            Go-ECM-dout -dout                       \ 關閉 Go_ECM dout
            -switch-on-dc                           \ 關閉電源系統
            1 GoECM-end-step +!                     \ 切換至下一步
        endof

        \ 等待振動與放電停止
        2 of
            ECM-done-ch-slv ec-din@ not             \ ECM_done din = false
            if
                1 GoECM-end-step +!                 \ 切換至下一步
            then
        endof

        \ 處理 aqua 延遲關閉
        3 of
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
            Go-ECM-dout -dout                     \ 關閉 Go_ECM dout
            -switch-on-dc                           \ 關閉munk電源
            1 GoECM-abort-step +!                   \ 切換至下一步
        endof

        \ 等待
        2 of
            ECM-done-ch-slv ec-din@ not             \ ECM_done din = false
            if
                1 GoECM-abort-step +!                   \ 切換至下一步
            then
        endof

        \ 開啟量測電路
        3 of
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
    PECM-going @
    motion-status @ serving = and
    cutting? and
;
transition GoECM-start?

\ 是否中斷 start 流程?
: GoECM-start-abort? ( -- flag )
    PECM-going @ not                 \ 若 start 流程中 PECM-going 被關掉
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
    PECM-going @ not                 \ 若加工中 PECM-going 被關掉

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
    PECM-going @ not                 \ 若 PECM-going 被關掉

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


marker -work