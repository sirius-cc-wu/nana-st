\ ==================== 備註 ====================
\ 此處還有些測試用命令，上機後需移除。

-work

\ ==================== Configurations ====================
0 constant serving
1 constant stopping
2 constant ending
3 constant emergency-stopping
4 constant stopped
0 constant none
1 constant nc
2 constant laser
3 constant trace

variable motion-status      \ serving/stopping/ending/emergency-stopping/stopped
stopped motion-status !
variable machining-mode     \ none/nc/laser/trace
variable v-axis-corrector  v-axis-corrector on
variable measure-go
variable measure-ready
variable measure-in-setting
variable measure-fault
variable touch-ignore
variable remoter-measure?
variable edge-meas-go
variable edge-meas-axis
fvariable edge-meas-dis

variable touch-buzzer       continuous-beep touch-buzzer !
variable nc-end-buzzer      single-solid-beep nc-end-buzzer !

fvariable laser-paused-cmd-p
fvariable path-hold-p
fvariable X-tracing-p
fvariable Y-tracing-p
fvariable Z-tracing-p
fvariable X-prev-demand-p
fvariable Y-prev-demand-p
fvariable Z-prev-demand-p
fvariable V-prev-demand-p

variable move-v-count     \ 於撞機等情況下, 用於移動v軸一次, 避免一直位移v軸
fvariable v-move-distance 30e um v-move-distance f!
: +v-axis-corrector
    v-axis-corrector on
;
: -v-axis-corrector
    v-axis-corrector off
;

\ 向量執行 RMT-led!
variable 'RMT-led!
: RMT-led! ( bool key -- )
    'RMT-led! @ execute
;



\ ==================== status ====================
\ 印出 ECM-motion-state 對應的狀態
: .motion-s ( -- )
    ECM-motion-state @ 
    case
        idle        of ." motion_state|Idle" cr endof
        jogging     of ." motion_state|Jogging" cr endof
        machining   of ." motion_state|Machining" cr endof
        meas        of ." motion_state|Measure" cr endof
    endcase
;

\ 回傳是否在 feedhold 中
: feedhold? ( -- flag )
    ECM-motion-state @ 0=           \ motion idle
    machining-mode @ none <> and    \ 且 machining mode 不為 none
;

\ 印出 ECM-feedhold 狀態
: .feedhold ( -- )
    feedhold?
    if
        ." feedhold|true" cr
    else
        ." feedhold|false" cr
    then
;

\ 印出 ECM-power-state 狀態
: .power-on ( -- )
    ECM-power-state @
    if
        ." power_on|true" cr
    else
        ." power_on|false" cr
    then
;

\ 印出緊急停止狀態
: .EMS ( -- )
    is-EMS?
    if
        ." EMS|true" cr
    else
        ." EMS|false" cr
    then
;

\ 印出進給率
: .feed-ratio ( -- )
    ." feed_ratio|" feed-ratio f@ 0 2 f.r cr
;

\ 印出加工是否短路
: .cutting-short ( -- )
    cutting-short?
    if
        ." cutting_short|true" cr
    else
        ." cutting_short|false" cr
    then
;

\ 回傳是否有任一軸碰到軟體極限
: ECM-on-sl? ( -- flag )
    ECM-X-axis dup axis-demand-p@ on-axis-psl
    ECM-X-axis dup axis-demand-p@ on-axis-nsl or
    ECM-Y-axis dup axis-demand-p@ on-axis-psl or
    ECM-Y-axis dup axis-demand-p@ on-axis-nsl or
    ECM-Z-axis dup axis-demand-p@ on-axis-psl or
    ECM-Z-axis dup axis-demand-p@ on-axis-nsl or
    ECM-V-axis dup axis-demand-p@ on-axis-psl or
    ECM-V-axis dup axis-demand-p@ on-axis-nsl or
    ECM-V-axis dup axis-real-p@ on-axis-psl or
    ECM-V-axis dup axis-real-p@ on-axis-nsl or
;

\ 回傳該軸是否有碰觸軟體極限
: ECM-axis-on-sl? ( axis-no -- flag )
    dup 4 =
    if
        dup axis-real-p@
    else
        dup axis-demand-p@
    then
    dup fdup on-axis-psl
    swap on-axis-nsl or
;

\ 紀錄 path hold position
: !path-hold-p ( -- )
    1 group! next-path-p@ path-hold-p f!
;

\ 紀錄軸組路徑停留的 acs position
: !tracing-p ( -- )
    ECM-X-axis axis-cmd-p@ X-tracing-p f!
    ECM-Y-axis axis-cmd-p@ Y-tracing-p f!
    ECM-Z-axis axis-cmd-p@ Z-tracing-p f!
;

\ 回傳 X,Y,Z 軸是否在 tracing position
: at-tracing-p? ( -- flag )
    ECM-X-axis axis-cmd-p@ X-tracing-p f@ f- fabs 0.5e um f<
    ECM-Y-axis axis-cmd-p@ Y-tracing-p f@ f- fabs 0.5e um f< and
    ECM-Z-axis axis-cmd-p@ Z-tracing-p f@ f- fabs 0.5e um f< and
;

\ 回傳該軸是否有 OT
\ 因緊急停止訊號與二道極限訊號同接在驅動器 DI7 腳，所以需看 beckhoff 上緊急停止訊號狀態才能正確判斷 OT 狀態。
: ECM-axis-OT? ( axis-no -- flag )
    is-EMS? not swap
    axis-drive@ drive-dins@ $2000000 and 0<>
    and
;

\ 回傳是否有 X, Y, Z 任一軸 OT
: ECM-has-OT? ( -- flag )
    ECM-X-axis ECM-axis-OT?
    ECM-Y-axis ECM-axis-OT? or
    ECM-Z-axis ECM-axis-OT? or
;

\ 回傳量測電路是否工作中
: measure-on? ( -- flag )
    measure-in-setting @ not
    measure-ready @ and
;

\ 回傳量測電路是否關閉中
: measure-off? ( -- flag )
    measure-in-setting @ not
    measure-ready @ not and
;

\ 回傳是否發生量測短路
: is-short? ( -- flag )
    measure-on? not             \ 量測電路未啟動
    short-signal? or            \ 或有量測短路訊號
;

\ 回傳是否發生碰邊
: is-touch? ( -- flag )
    touch-ignore @ not          \ 未忽略碰邊
    is-short? and               \ 且發生量測短路
;

\ 回傳是否在 NC 中需忽略碰邊的路徑上
: touch-ignore-in-nc? ( -- flag )
    goecm-in-idle @ not         \ 放電中
    touch-ignore-line? or       \ 或忽略碰邊路徑
;

\ 回傳 PEM 是否 ready
: PEM-ready? ( -- flag )
    \ TODO: 暫時都回傳 true
    \ PEM-Ready-to-work@ 0<>
    \ PORT+OVP+TMP-WAR@ not and
    true
;

\ 回傳 DSP 是否 ready
: DSP-ready? ( -- flag )
    DSP-fault @ not
    DSP-suspend @ not and
;

\ 回傳 v 軸是否已回正
: v-axis-corrected? ( -- flag )
    ECM-V-axis axis-cmd-p@ fabs 0.1e um f> not
;



\ ==================== measure ====================
variable 'meas-touch
' is-touch? 'meas-touch !

\ 這是給量測活動使用的，回傳是否發生碰邊
: meas-touch? ( -- flag )
    'meas-touch @ execute
;

\ 是否需緊急停止量測?
: EMS-in-meas? ( -- flag )
    motion-status @ emergency-stopping <    \ motion status 為 emergency stopping 以前
    if
        ECM-power-state @ not               \ power off
        ECM-on-sl? or                       \ 或觸發到極限開關
        measure-on? not or                  \ 或量測電路非工作中
    else
        false
    then
;

\ 印出導致量測中需緊急停止的異警
: .meas-EMS-errors
    ECM-power-state @ not if
        ." error|Unexpected power off. Stop measure.;A1312" cr
    then
    ECM-on-sl? if
        ." error|Software limit reached. Stop measure.;A1313" cr
    then
    measure-on? not
    if
        ." error|Short protection curcuit unexpected disabled. Stop measure.;A1314" cr
    then
;



\ ==================== jog ====================
\ 回傳是否應該停止已碰觸軟體極限的軸移動命令，即已碰觸軟體極限之軸欲往同方向移動。
: stop-jogging-on-sl? ( -- flag )
    ECM-X-axis dup axis-demand-p@ on-axis-psl
    ECM-X-axis axis-demand-p@ X-prev-demand-p f@ f> and

    ECM-X-axis dup axis-demand-p@ on-axis-nsl
    ECM-X-axis axis-demand-p@ X-prev-demand-p f@ f< and
    or

    ECM-Y-axis dup axis-demand-p@ on-axis-psl
    ECM-Y-axis axis-demand-p@ Y-prev-demand-p f@ f> and
    or

    ECM-Y-axis dup axis-demand-p@ on-axis-nsl
    ECM-Y-axis axis-demand-p@ Y-prev-demand-p f@ f< and
    or

    ECM-Z-axis dup axis-demand-p@ on-axis-psl
    ECM-Z-axis axis-demand-p@ Z-prev-demand-p f@ f> and
    or

    ECM-Z-axis dup axis-demand-p@ on-axis-nsl
    ECM-Z-axis axis-demand-p@ Z-prev-demand-p f@ f< and
    or

    ECM-V-axis dup axis-demand-p@ on-axis-psl
    ECM-V-axis axis-demand-p@ V-prev-demand-p f@ f> and
    or

    ECM-V-axis dup axis-demand-p@ on-axis-nsl
    ECM-V-axis axis-demand-p@ V-prev-demand-p f@ f< and
    or
;

\ 回傳該軸是否沒有碰觸軟體極限或碰觸軟體極限但往相反方向移動
: jog-accepted-by-sl? ( axis-no -- flag ) ( F: dis -- )
    dup dup axis-demand-p@ on-axis-psl
    fdup 0e f> and                          \ 該軸碰觸正極限且欲往正方向移動

    swap dup axis-demand-p@ on-axis-nsl
    0e f< and
    or                                      \ 或該軸碰觸負極限且欲往負方向移動
    not                                     \ 反向
;

\ 回傳是否發生 jogging 中需停止的異警
: stop-in-jogging? ( -- flag )
    motion-status @ serving =               \ motion status 為 serving
    if
        stop-jogging-on-sl?                 \ 或碰觸軟體極限且需停止
    else
        false
    then
;

\ 印出導致 jogging 中需停止的異警
: .jogging-stop-errors
    stop-jogging-on-sl?
    if
        ." error|Software limit reached. Stop jogging.;A1201" cr
    then
;

\ 回傳是否發生 jogging 中需緊急停止的異警
: EMS-in-jogging? ( -- flag )
    motion-status @ emergency-stopping <    \ motion status 為 emergency stopping 以前
    if
        ECM-power-state @ not               \ power off
        is-touch? or                        \ 或發生碰邊
    else
        false
    then
;

\ 印出導致 jogging 中需緊急停止的異警
: .jogging-EMS-errors
    ECM-power-state @ not
    if
        ." error|Unexpected power off. Stop jogging.;A1202" cr
    then
    is-touch?
    if
        measure-on?
        if
            ." error|Touched. Stop jogging.;A1203" cr
        else
            ." error|Short protection circuit unexpected disabled. Stop jogging.;A1204" cr
        then
    then
;

\ 回傳是否可以進行軸移動
: jog-allowed? ( axis-no -- flag ) ( F: distance -- )
    jog-accepted-by-sl?                     \ 該軸沒有碰觸軟體極限或碰觸軟體極限但往相反方向移動
    dup not if
        ." error|Jog on software limit unaccepted. Jog failed.;A1205" cr
    then

    ECM-power-state @ and                   \ 且 power on 中
    is-EMS? not and                         \ 且 EMS 未觸發
    is-touch? not and                       \ 且沒有發生碰邊

    feedhold?
    if                                      \ 為 feedhold
        machining-mode @ dup nc =               \ machining mode 為 nc
        swap trace = or                         \ 或 trace
    else                                    \ 不為 feedhold
        ECM-motion-state @ dup idle =           \ 為 idle
        swap jogging = or                       \ 或 jogging
    then
    and
;

\ 印出導致 jog 失敗的錯誤
: .jog-errors
    ECM-power-state @ not
    if
        ." error|Not power on. Jog failed.;A1206" cr
    then
    is-EMS?
    if
        ." error|In EMS. Jog failed.;A1207" cr
    then
    is-touch?
    if
        measure-on?
        if
            ." error|In touch. Jog failed.;A1208" cr
        else
            ." error|Short protection circuit not enabled. Jog failed.;A1209" cr
        then
    then

    feedhold?
    if
        machining-mode @ dup nc = swap trace = or not
        if
            ." error|Not in nc or trace mode. Jog failed.;A1210" cr
        then
    else
        ECM-motion-state @ dup idle <>
        swap jogging <> and
        if
            ." error|ECM neither idle nor jogging. Jog failed.;A1211" cr
        then
    then
;

\ 軸移動
: jog ( axis-no -- ) ( F: dis -- )
    dup fdup jog-allowed?
    if
        serving motion-status !         \ 設定為 serving
        jogging ECM-motion-state !      \ 設定為 jogging
        dup +interpolator               \ 開啟該軸的差值器
        dup axis-cmd-p@ f+ axis-cmd-p!  \ 軸移動

        machining-mode @ nc =           \ 若 machining mode 為 nc
        if
            trace machining-mode !      \ 設定為 trace
        then
    else
        .jog-errors
        drop fdrop
    then
;

\ 設定差值器速度限制
: jog-v! ( axis-no -- ) ( F: velocity -- )
    ECM-power-state @                   \ power on 中
    ECM-motion-state @ machining <> and \ 且不是 motion machining
    if
        interpolator-v!
    else
        drop fdrop
    then
;

\ 檢查 v 軸的 cmd-p，若未回正就進行回正
: correct-v-axis-cmd-p
    \ 限制 0.1um < |cmd-p| < 1mm 才進行回正
    ECM-V-axis axis-cmd-p@ fabs fdup 0.1e um f> 1e mm f< and
    if
        ECM-power-state @                   \ power on 中
        is-EMS? not and                     \ 且 EMS 未觸發
        measure-on? and                     \ 且量測電路工作中
        touch-ignore @ not and              \ 且未忽略碰邊
        is-short? not and                   \ 且沒有發生量測短路
        ECM-motion-state @ idle = and       \ 且 motion idle
        feedhold? not and                   \ 且不是 feedhold
        if
            serving motion-status !         \ 設定為 serving
            jogging ECM-motion-state !      \ 設定為 jogging
            ECM-V-axis  dup 60e mm/min interpolator-v!  dup +interpolator  0e axis-cmd-p!
        then
    then
;

: v-force-move
    move-v-count @ 1 = if
        ECM-power-state @                   \ power on 中
        is-EMS? not and                     \ 且 EMS 未觸發
        measure-on? and                     \ 且量測電路工作中
        touch-ignore @ not and              \ 且未忽略碰邊
        is-short? not and                   \ 且沒有發生量測短路
        ECM-motion-state @ idle = and       \ 且 motion idle
        feedhold? not and                   \ 且不是 feedhold
        if
            serving motion-status !         \ 設定為 serving
            jogging ECM-motion-state !      \ 設定為 jogging
            ECM-V-axis  dup 60e mm/min interpolator-v!  dup +interpolator ECM-V-axis axis-real-p@ v-move-distance f@ f+ axis-cmd-p!
            2 move-v-count !
        then
    then
;


\ ==================== machining ====================
\ 回傳是否發生 machining 中需暫停工作的異警
: stop-in-machining? ( -- flag )
    motion-status @ serving =           \ motion status 為 serving
    if
        machining-mode @
        case
            nc of
                touch-ignore-in-nc? not \ 未在需忽略碰邊的路徑上
                is-touch? and           \ 且發生碰邊

                platform-door@ not or   \ 或 CNC 安全門未緊閉
                GoECM-fault @ or        \ 或發生 GoECM fault
                GoECM-V-fault @ or      \ 或振動軸位置變形造成無法放電
            endof

            laser of
                is-touch?               \ 發生碰邊
            endof

            trace of
                ECM-power-state @ not   \ power off
                ECM-on-sl? or           \ 或碰觸軟體極限
                is-touch? or            \ 或發生碰邊
            endof

            ." runtime_error|Unreachable case in stop-in-machining?" cr
        endcase
    else
        false
    then
;

\ 印出導致 machining 中需暫停工作的異警
: .machining-stop-errors
    machining-mode @
    case
        nc of
            touch-ignore-in-nc? not
            is-touch? and
            if
                measure-on?
                if
                    ." error|Touched. Stop NC.;A1501" cr
                else
                    ." error|Short pretection circuit unexpected disabled. Stop NC.;A1502" cr
                then
            then
            
            is-PROT?
            if
            ." error| PEM power Prot.;A3201" cr
            then

            is-OVP?
            if
            ." error| PEM power OVP(Over-Voltage Protection).;A3202" cr
            then

            is-TmpWarn?
            if
            ." error| PEM power Temp Warning(Over-temperature).;A3203" cr
            then

            is-NegVol?
            if
            ." error| Negative Voltage.;A3204" cr
            then

            is-CircuitFail?
            if
            ." error| Circuit Fail.;A3205" cr
            then

            platform-door@ not
            if
                ." error|CNC security door not closed. Stop NC.;A1503" cr
            then
            GoECM-fault @
            if
                ." error|Electric discharging fault. Stop NC.;A1504" cr
            then
            GoECM-V-fault @
            if
                ." error|V axis position following error. Discharging fault. Stop NC.;A1505" cr
            then
        endof

        laser of
            is-touch?
            if
                measure-on?
                if
                    ." error|Touched. Stop laser correction.;A1401" cr
                else
                    ." error|Short pretection circuit unexpected disabled. Stop laser correction.;A1402" cr
                then
            then
        endof

        trace of
            ECM-power-state @ not
            if
                ." error|Unexpected power off. Stop trace.;A1506" cr
            then
            ECM-on-sl?
            if
                ." error|Software limit reached. Stop trace.;A1507" cr
            then
            is-touch?
            if
                measure-on?
                if
                    ." error|Touched. Stop trace.;A1508" cr
                else
                    ." error|Short pretection circuit unexpected disabled. Stop trace.;A1509" cr
                then
            then
        endof
    endcase
;

\ 回傳是否發生 machining 中需緊急停止的異警
: EMS-in-machining? ( -- flag )
    motion-status @ emergency-stopping <    \ motion status 為 emergency stopping 以前
    if
        machining-mode @
        case
            nc of
                ECM-power-state @ not       \ power off
                ECM-on-sl? or               \ 或碰觸軟體極限
                PEM-ready? not or           \ 或 PEM not ready
                DSP-ready? not or           \ 或 DSP not ready
            endof

            laser of
                ECM-power-state @ not               \ power off
                laser-axis @ ECM-axis-on-sl? or     \ 或雷射補正軸碰觸軟體極限
            endof

            trace of
                false
            endof

            ." runtime_error|Unreachable case in EMS-in-machining?" cr
        endcase
    else
        false
    then
;

\ 印出導致 machining 中需緊急停止的異警
: .machining-EMS-errors
    machining-mode @
    case
        nc of
            ECM-power-state @ not
            if
                ." error|Unexpected power off. Abort NC.;A1510" cr
            then
            ECM-on-sl?
            if
                ." error|Software limit reached. Abort NC.;A1511" cr
            then
            PEM-ready? not
            if
                ." error|PEM not ready. Abort NC.;A1512" cr
            then
            DSP-ready? not
            if
                ." error|DSP not ready. Abort NC.;A1513" cr
            then
        endof

        laser of
            ECM-power-state @ not
            if
                ." error|Unexpected power off. Abort laser correction.;A1403" cr
            then
            laser-axis @ ECM-axis-on-sl?
            if
                ." error|Software limit reached. Abort laser correction.;A1404" cr
            then
        endof

        trace of
            ." runtime_error|Unreachable case in .machining-EMS-faults." cr
        endof
    endcase
;

\ 回傳是否可以執行 nc 程式
: nc-allowed? ( -- flag )
    ECM-power-state @               \ power on 中
    ECM-motion-state @ idle = and   \ 且 motion idle
    feedhold? not and               \ 且不是 feedhold
    is-EMS? not and                 \ 且 EMS 未觸發
    ECM-on-sl? not and              \ 且未碰觸軟體極限
    is-touch? not and               \ 且未發生碰邊
    PEM-ready? and                  \ 且 PEM ready
    DSP-ready? and                  \ 且 DSP ready
    platform-door@ and              \ 且 CNC 安全門緊閉
    v-axis-corrected? and           \ 且 v 軸已回正
;

\ 印出導致 nc-go 失敗的原因
: .nc-go-errors
    ECM-power-state @ not
    if
        ." error|Not power on. Start NC failed.;A1514" cr
    then
    ECM-motion-state @ idle <>
    if
        ." error|ECM not idle. Start NC failed.;A1515" cr
    then
    feedhold?
    if
        ." error|ECM in feedhold. Start NC failed.;A1516" cr
    then
    is-EMS?
    if
        ." error|In EMS. Start NC failed.;A1517" cr
    then
    ECM-on-sl?
    if
        ." error|On software limit. Start NC failed.;A1518" cr
    then
    is-touch?
    if
        measure-on?
        if
            ." error|In touch. Start NC failed.;A1519" cr
        else
            ." error|Short protection circuit not enabled. Start NC failed.;A1520" cr
        then
    then
    PEM-ready? not
    if
        ." error|PEM not ready. Start NC failed.;A1521" cr
    then
    DSP-ready? not
    if
        ." error|DSP not ready. Start NC failed.;A1522" cr
    then
    platform-door@ not
    if
        ." error|CNC security door not closed. Start NC failed.;A1523" cr
    then
    v-axis-corrected? not
    if
        ." error|V axis position not returned. Start NC failed.;A1524" cr
    then
;

\ 宣告開始加工
: nc-go ( -- )
    nc-allowed?
    if
        reset-job start-job             \ 初始化工作
        ECM-going on                    \ 開啟 GoECM
        +g-led                          \ 三色燈切換為綠燈
        start-LED-ch-slv +dout          \ 開啟控制面板 start 燈號
        1 1 RMT-led!                    \ 開啟遙控器 start 按鍵燈號
        machining ECM-motion-state !    \ 設定為 machining
        nc machining-mode !             \ 設定為 NC
        serving motion-status !         \ 設定為 serving
        ." log|Start NC." cr
    else
        .nc-go-errors
        kill-nc
    then
;

\ 宣告加工完成。
: end-of-nc ( -- )
    ending motion-status max!           \ 切換到 ending
;
\ 將 end-of-nc 令牌放到 end-of-nc-xt 中
' end-of-nc end-of-nc-xt !

\ 回傳該軸是否可以進行雷射補正運動
: laser-allowed? ( axis-no -- flag )
    ECM-axis-on-sl? not             \ 該軸沒有碰觸軟體極限
    dup not if
        ." error|Axis on software limit. Start laser correction failed.;A1405" cr
    then

    ECM-power-state @ and           \ 且 power on 中
    is-EMS? not and                 \ 且 EMS 未觸發
    is-touch? not and               \ 且沒有發生碰邊
    ECM-motion-state @ idle = and   \ 且 motion idle
    feedhold? not and               \ 且不是 feedhold
;

\ 印出導致 laser go 失敗的原因
: .laser-go-errors
    ECM-power-state @ not
    if
        ." error|Not power on. Start laser correction failed.;A1406" cr
    then
    is-EMS?
    if
        ." error|In EMS. Start laser correction failed.;A1407" cr
    then
    is-touch?
    if
        measure-on?
        if
            ." error|In touch. Start laser correction failed.;A1408" cr
        else
            ." error|Short protection circuit not enabled. Start laser correction failed.;A1409" cr
        then
    then
    ECM-motion-state @ idle <>
    if
        ." error|ECM not idle. Start laser correction failed.;A1410" cr
    then
    feedhold?
    if
        ." error|ECM in feedhold. Start laser correction failed.;A1411" cr
    then
;

\ 進行雷射補正運動
: laser-go ( axis count ms -- ) ( F: start-pos step feedrate backlash -- )
    2 pick laser-allowed?
    if
        +g-led                                      \ 三色燈切換為綠燈
        start-LED-ch-slv +dout                      \ 開啟控制面板 start 燈號
        1 1 RMT-led!                                \ 開啟遙控器 start 按鍵燈號
        machining ECM-motion-state !                \ 設定為 machining
        laser machining-mode !                      \ 設定為 laser
        serving motion-status !                     \ 設定為 serving
        ." log|Start laser correction." cr
        laser-fb                                    \ 執行雷射補正運動

        ending motion-status max!                   \ 切換到 ending
        ." log|Laser correction finished." cr
    else
        .laser-go-errors
        drop drop drop
        fdrop fdrop fdrop fdrop
    then
;

\ 回傳是否可以執行 restart-motion
: restart-motion-allowed? ( -- flag )
    feedhold?                   \ feedhold 中
    if
        machining-mode @
        case
            nc of
                ECM-power-state @       \ power on 中
                is-touch? not and       \ 且沒有發生碰邊
                PEM-ready? and          \ 且 PEM ready
                DSP-ready? and          \ 且 DSP ready
                platform-door@ and      \ 且 CNC 安全門緊閉
                at-tracing-p? and       \ 且 X,Y,Z 軸在 tracing position
            endof
            
            laser of
                ECM-power-state @       \ power on 中
                is-touch? not and       \ 且沒有發生碰邊
            endof

            trace of
                ECM-power-state @       \ power on 中
                is-touch? not and       \ 且沒有發生碰邊
                PEM-ready? and          \ 且 PEM ready
                DSP-ready? and          \ 且 DSP ready
                platform-door@ and      \ 且 CNC 安全門緊閉
                at-tracing-p? and       \ 且 X,Y,Z 軸在 tracing position
            endof
        endcase
    else
        false
    then
;

\ 印出導致 restart motion 失敗的錯誤
: .restart-errors
    feedhold? not
    if
        ." error|Not in feedhold. Restart job failed.;A1525" cr
    then
    machining-mode @
    case
        nc of
            ECM-power-state @ not
            if
                ." error|Not power on. Restart NC failed.;A1526" cr
            then
            is-touch?
            if
                measure-on?
                if
                    ." error|In touch. Restart NC failed.;A1527" cr
                else
                    ." error|Short protection circuit not enabled. Restart NC failed.;A1528" cr
                then
            then
            PEM-ready? not
            if
                ." error|PEM not ready. Restart NC failed.;A1529" cr
            then
            DSP-ready? not
            if
                ." error|DSP not ready. Restart NC failed.;A1530" cr
            then
            platform-door@ not
            if
                ." error|CNC security door not closed. Restart NC failed.;A1531" cr
            then
            at-tracing-p? not
            if
                ." error|Jogged axis not returned to tracing point. Restart NC failed.;A1532" cr
            then
        endof

        laser of
            ECM-power-state @ not
            if
                ." error|Not power on. Restart laser correction failed.;A1412" cr
            then
            is-touch?
            if
                measure-on?
                if
                    ." error|In touch. Restart laser correction failed.;A1413" cr
                else
                    ." error|Short protection circuit not enabled. Restart laser correction failed.;A1414" cr
                then
            then
        endof

        trace of
            ECM-power-state @ not
            if
                ." error|Not power on. Restart NC failed.;A1533" cr
            then
            is-touch?
            if
                measure-on?
                if
                    ." error|In touch. Restart NC failed.;A1534" cr
                else
                    ." error|Short protection circuit not enabled. Restart NC failed.;A1535" cr
                then
            then
            PEM-ready? not
            if
                ." error|PEM not ready. Restart NC failed.;A1536" cr
            then
            DSP-ready? not
            if
                ." error|DSP not ready. Restart NC failed.;A1537" cr
            then
            platform-door@ not
            if
                ." error|CNC security door not closed. Restart NC failed.;A1538" cr
            then
            at-tracing-p? not
            if
                ." error|Jogged axis not returned to tracing point. Restart NC failed.;A1539" cr
            then
        endof
    endcase
;

\ 重新開始 task1 的工作
: restart-motion ( -- )
    restart-motion-allowed?
    if
        machining-mode @
        case
            nc of
                feed-ratio-old f@ feed-ratio f!     \ 設定調變百分比為暫停前的值
                ECM-going on                        \ 啟動 GoECM
                1 group! +group gstart              \ 啟動 group1
                1 resume                            \ 喚醒 task1
            endof

            laser of
                laser-axis @ +interpolator          \ 開啟雷射補正軸的差值器
                laser-axis @ laser-paused-cmd-p f@ axis-cmd-p!  \ 設定命令位置為暫停前的命令位置
                1 resume                            \ 喚醒 task1
            endof

            trace of
                feed-ratio-old f@ feed-ratio f!     \ 設定調變百分比為暫停前的值
                ECM-going on                        \ 啟動 GoECM
                1 group! rapid-traverse-rate@ vcmd! \ 設定 vcmd 為正
                +group gstart                       \ 啟動 group1
                1 resume                            \ 喚醒 task1
                nc machining-mode !                 \ 設定為 nc
            endof

            ." runtime_error|Unreachable case2 in restart-motion." cr
        endcase

        +g-led                          \ 三色燈切換為綠燈
        start-LED-ch-slv +dout          \ 開啟控制面板 start 燈號
        1 1 RMT-led!                    \ 開啟遙控器 start 按鍵燈號
        pause-LED-ch-slv -dout          \ 關閉 pause 燈號
        machining ECM-motion-state !    \ 設定為 machining
        serving motion-status !         \ 設定為 serving
        ." log|Job restarted." cr
    else
        .restart-errors
    then
;



\ ==================== stop/EMS/abort ====================
\ 停止命令根據 ECM-motion-state 判斷該做什麼事
: stop-motion ( -- )
    motion-status @ serving =           \ motion status 為 serving
    if
        ECM-motion-state @
        case
            jogging of
                stop-job                        \ 停止運動
            endof

            machining of
                machining-mode @
                case
                    nc of
                        1 group! gstop          \ 停止 group1
                        ECM-going off           \ GoECM 休眠
                        1 suspend               \ 暫停 task1
                    endof

                    laser of
                        laser-axis @ axis-cmd-p@ laser-paused-cmd-p f!  \ 紀錄當下 cmd-p
                        stop-job                \ 停止運動
                        1 suspend               \ 暫停 task1
                    endof

                    trace of
                        stop-job                \ 停止運動
                    endof

                    ." runtime_error|Unreachable case2 in stop-motion." cr
                endcase
                
                pause-LED-ch-slv +dout          \ 開啟控制面板 pause 燈號
                1 2 RMT-led!                    \ 開啟遙控器 pause 按鍵燈號
                ." log|Job stopped." cr
            endof

            meas of
                stop-job                        \ 停止運動
                kill-nc                         \ TODO:目前 meas 沒有規劃暫停重啟的行為，故放棄工作
                ." log|Measuring aborted." cr
            endof

            ." runtime_error|Unreachable case1 in stop-motion." cr
        endcase

        stopping motion-status max!             \ 切換到 stopping
    then
;

\ 視情況緊急停止運動
: EMS-motion
    ECM-motion-state @
    case
        idle of
            ( do nothing )
        endof

        jogging of
            ecm-x-axis ems-axis
            ecm-y-axis ems-axis
            ecm-z-axis ems-axis
            ecm-v-axis ems-axis
        endof

        machining of
            ems-job                             \ 緊急停止工作
            ECM-going off                       \ GoECM 休眠
            kill-nc                             \ 重置 NC task
            pause-LED-ch-slv -dout              \ 關閉控制面板 pause 燈號
            0 2 RMT-led!                        \ 關閉遙控器 pause 按鍵燈號
            ." log|Job aborted." cr
        endof

        meas of
            ems-job
            kill-nc
            ." log|Measuring aborted." cr
        endof
    endcase

    emergency-stopping motion-status max!       \ 切換到 emergency stopping
;

\ 無區別緊急停止運動
: EMS-motion-undiff
    ems-job                                 \ 緊急停止所有 group 和 axis 並清除所有路徑
    ECM-going off                           \ GoECM 休眠
    kill-nc                                 \ 重置 NC task
    pause-LED-ch-slv -dout                  \ 關閉控制面板 pause 燈號
    0 2 RMT-led!                            \ 關閉遙控器 pause 按鍵燈號

    feedhold?                               \ 若為 feedhold
    if
        none machining-mode !                   \ 清除 machining 工作
    then

    emergency-stopping motion-status max!   \ 切換到 emergency stopping
    ." log|Emergency stopped." cr
    ." log|Job aborted." cr
;

\ 中斷目前的運動控制工作
: abort-motion
    stop-job                            \ 停止工作
    ECM-going off                       \ GoECM 休眠
    kill-nc                             \ 重置 NC task
    pause-LED-ch-slv -dout              \ 關閉控制面板 pause 燈號
    0 2 RMT-led!                        \ 關閉遙控器 pause 按鍵燈號

    feedhold?                           \ 若 feedhold 中就強制回 none
    if
        none machining-mode !
    then

    ending motion-status max!           \ 切換到 ending
    ." log|Job aborted." cr
;

\ 處理正反器
: motion-state-ffs-forth ( -- )
    \ 緊急停止按鈕正反器
    is-EMS? ff-ems-re ff-forth-uc

    ff-ems-re ff-triggered-uc?          \ 若緊急停止按鈕的正反器觸發
    if
        EMS-motion-undiff                   \ 無區別緊急停止運動
    then

    \ system ready 後才開始處理的
    system-ready?
    if
        \ 碰邊事件正反器
        ECM-motion-state @ machining =
        machining-mode @ nc = and           \ 進行 NC 中
        touch-ignore-in-nc? and not         \ 且在需忽略碰邊的路徑上的要忽略掉
        is-touch? and                       \ 碰邊
        dup
        ff-touch-re ff-forth-uc
        ff-touch-fe ff-forth-uc

        ff-touch-re ff-triggered-uc?        \ 若碰邊 rising edge 觸發
        if
            touch-buzzer set-buzzer             \ 設置蜂鳴器
            1 42 rmt-led!                       \ 開啟遙控器 touch LED    
        then

        ff-touch-fe ff-triggered-uc?        \ 若碰邊 falling edge 觸發
        if
            touch-buzzer unset-buzzer           \ 關閉蜂鳴器
            0 42 rmt-led!                       \ 關閉遙控器 touch LED
        then

        \ 量測短路事件正反器
        ECM-motion-state @ machining =
        machining-mode @ nc = and
        cutting? and not                    \ 在 NC 中且在放電路徑上的需忽略掉
        is-short? and                       \ 量測短路
        dup
        ff-short-re ff-forth-uc
        ff-short-fe ff-forth-uc

        ff-short-re ff-triggered-uc?        \ 若量測短路 rising edge 觸發
        if
            1 41 rmt-led!                       \ 開啟遙控器 short LED
        then

        ff-short-fe ff-triggered-uc?        \ 若量測短路 falling edge 觸發
        if
            0 41 rmt-led!                       \ 關閉遙控器 short LED
        then
    then
;

\ 更新 motion state
: motion-state-forth ( -- )
    ECM-motion-state @
    case
        idle of
            v-axis-corrector @
            if
                correct-v-axis-cmd-p            \ 處理 v 軸 cmd-p 檢查與回正
            else
                v-force-move                    \ v軸根據v-rise-distance做移動
            then
        endof

        jogging of
            EMS-in-jogging?                     \ 若發生 jogging 中需急停的異警
            if
                EMS-motion                          \ 緊急停止
                .jogging-EMS-errors                 \ 印出異警訊息
            else
                stop-in-jogging?                    \ 否則若發生 jogging 中需停止的異警
                if
                    stop-motion                         \ 停止
                    .jogging-stop-errors                \ 印出異警訊息
                else
                    job-stop?                           \ 否則若運動已停止
                    if
                        stop-job                            \ 關閉所由軸的差值器
                        touch-ignore off                    \ 關閉忽略碰邊
                        stopped motion-status !             \ 切換到 stopped
                        idle ECM-motion-state !             \ 切換到 idle
                    then
                then
            then
        endof

        machining of
            EMS-in-machining?                   \ 若發生 machining 中需急停的異警
            if
                EMS-motion                          \ 緊急停止
                .machining-EMS-errors               \ 印出異警訊息
            else
                stop-in-machining?                  \ 否則若發生 machining 中需暫停的異警
                if
                    stop-motion                         \ 暫停
                    .machining-stop-errors              \ 印出異警訊息
                else
                    motion-status @ serving <>          \ 否則若 motion status 不為 serving
                    job-stop? and                       \ 且運動已停止
                    GoECM-in-idle @ and                 \ 且 GoECM 已停止
                    if
                        stop-job                            \ 停止所有 group

                        motion-status @ stopping =          \ 若為 stopping 表示是工作暫停
                        if
                            machining-mode @ nc =               \ 若工作為 nc
                            if
                                !path-hold-p                        \ 紀錄路徑暫停位置
                                !tracing-p                          \ 紀錄軸組路徑停留的 acs position
                            then
                        else                                \ 否則表示工作已結束或放棄
                            reset-job                           \ 重置所有 group
                            ECM-going off                       \ GoECM 休眠
                            nc-end-buzzer set-buzzer            \ 設置蜂鳴器
                            none machining-mode !               \ 清除 machining 工作
                        then

                        1 group! -group                     \ 關閉 group1
                        start-LED-ch-slv -dout              \ 關閉控制面板 start 燈號
                        0 1 RMT-led!                        \ 關閉遙控器 start 按鍵燈號
                        +y-led                              \ 三設燈切換為 yellow
                        touch-ignore off                    \ 關閉忽略碰邊
                        stopped motion-status !             \ 設定為 stopped
                        idle ECM-motion-state !             \ 切換到 idle
                    then
                then
            then
        endof

        meas of
            EMS-in-meas?                        \ 若發生 meas 中需急停的異警
            if
                EMS-motion                          \ 緊急停止
                .meas-EMS-errors                    \ 印出異警訊息
            else
                meas-group group! gstop? not
                meas-touch? and
                if
                    \ [TODO] 可以紀錄此時的 feedback position，
                    \ 考慮減速到零與落後誤差，此位置比較貼近真實的位置

                    \ 軸組停止移動
                    0e vcmd!
                    gstop
                else
                    \ 設定運動速度
                    rapid-traverse-rate@ vcmd!
                then

                motion-status @ serving <>          \ 若不為 serving
                job-stop? and                       \ 且運動已停止
                if
                    touch-ignore off                    \ 關閉忽略碰邊
                    reset-job                           \ 重置所有 group
                    meas-group group! -group            \ 關閉量測軸組
                    stopped motion-status !             \ 設定為 stopped
                    idle ECM-motion-state !             \ 切換到 idle
                then
            then
        endof
    endcase

    \ 隨時更新 previous demand position，用來在下出運動命令前，先判斷欲往哪個方向運動
    ECM-X-axis axis-demand-p@ X-prev-demand-p f!
    ECM-Y-axis axis-demand-p@ Y-prev-demand-p f!
    ECM-Z-axis axis-demand-p@ Z-prev-demand-p f!
    ECM-V-axis axis-demand-p@ V-prev-demand-p f!
;



\ ===== measure on/off macro =====
\ 說明：
\   此 macro 監聽 measure-go，AC20V-on? 狀態，處理量測電路開啟/關閉，和量測電路異常處理工作
\ 
\ use: measure-go, AC20V-on?
\ 
\ public: measure-ready, measure-in-setting, measure-fault
\ 
\ 正常流程：
\   1. 在 S0 等待時若監聽到 AC20V-on? = 0 且 measure-go = true，就開啟量測電路並等待 AC20V-on? != 0 
\   2-1. 等待到 measure-go = true 且 AC20V-on? != 0，表示開啟流程完成，至 S2 等待
\   2-2. 若等待過程中監聽到 measure-go = false，表示放棄開啟流程，前往 S3 進行關閉流程
\   3. 在 S2 等待時若監聽到 AC20V-on? != 0 且 measure-go = false，就關閉量測電路並等待 AC20V = 0
\   4-1. 等待到 measure-go = false 且 AC20V-on? = 0，表示關閉流程完成，至 S0 等待
\   4-2. 若等待過程中監聽到 measure-go = true，表示放棄關閉流程，前往 S1 進行開啟流程
\
\ 異常流程：
\   1. 若在 S0 等待時監聽到 measure-go = false 但 AC20V-on? != 0，表示量測電路異常開啟，開啟 measure-fault
\   旗標並前往 S3 進行關閉流程
\   2. 若在 S2 等待時監聽到 measure-go = true 但 AC20V-on? = 0，表示量測電路異常關閉，開啟 measure-fault 旗標
\   並前往 S1 進行開啟流程
\ 
\ SFC：
\ 
\        T0         T1         T2         T3
\ S0--+--+-->S1--+--+-->S2--+--+-->S3--+--+-->S0
\     |          |          |          |
\     |  T6      |  T4      |  T7      |  T5
\     +--+-->S3  +--+-->S3  +--+-->S1  +--+-->S1
\ 
\ ---+------------+---------------------------------------------------------------------------------
\ S0 | measure-S0 | waiting
\ ---+------------+---------------------------------------------------------------------------------
\ S1 | measure-S1 | 開啟量測電路
\ ---+------------+---------------------------------------------------------------------------------
\ S2 | measure-S2 | waiting
\ ---+------------+---------------------------------------------------------------------------------
\ S3 | measure-S3 | 關閉量測電路
\ ---+------------+---------------------------------------------------------------------------------
\ 
\ ---+------------+---------------------------------------------------------------------------------
\ T0 | measure-T0 | 判斷 AC20V-on? = 0，且 measure-go = true
\ ---+------------+---------------------------------------------------------------------------------
\ T1 | measure-T1 | 判斷 measure-go = true，且 AC20V-on? != 0
\ ---+------------+---------------------------------------------------------------------------------
\ T2 | measure-T2 | 判斷 AC20V-on? != 0，且 measure-go = false
\ ---+------------+---------------------------------------------------------------------------------
\ T3 | measure-T3 | 判斷 measure-go = false，且 AC20V-on? = 0
\ ---+------------+---------------------------------------------------------------------------------
\ T4 | measure-T4 | 判斷 measure-go = false
\ ---+------------+---------------------------------------------------------------------------------
\ T5 | measure-T5 | 判斷 measure-go = true
\ ---+------------+---------------------------------------------------------------------------------
\ T6 | measure-T6 | 判斷 AC20V-on? != 0
\ ---+------------+---------------------------------------------------------------------------------
\ T7 | measure-T7 | 判斷 AC20V-on? = 0
\ ---+------------+---------------------------------------------------------------------------------

variable measure-S1-done
variable measure-S3-done

: AC20V-on? 1 ; \ TODO: 計畫做在 DSP 回傳封包中的資料，此為暫時假資料

: measure-S0 ( waiting ) ;

: measure-S1
    measure-S1-done @ not
    if
        AC20V-control-ch-slv +dout
        measure-S1-done on
    then
;

: measure-S2 ( waiting ) ;

: measure-S3
    measure-S3-done @ not
    if
        AC20V-control-ch-slv -dout
        measure-S3-done on
    then
;

: measure-T0
    \ TODO: 暫時不判斷 AC20V 狀態
    \ AC20V-on? not
    measure-go @ \ and
    dup if
        measure-in-setting on
    then
;

: measure-T1
    measure-go @
    ['] measure-S1 elapsed 1000 > and
    \ TODO: 暫時不判斷 AC20V 狀態
    \ AC20V-on? 0<> and
    dup if
        measure-S1-done off
        measure-in-setting off
        measure-fault off
        measure-ready on
    then
;

: measure-T2
    \ TODO: 暫時不判斷 AC20V 狀態
    \ AC20V-on? 0<>
    measure-go @ not \ and
    dup if
        measure-in-setting on
    then
;

: measure-T3
    measure-go @ not
    \ TODO: 暫時不判斷 AC20V 狀態
    \ AC20V-on? not and
    dup if
        measure-S3-done off
        measure-in-setting off
        measure-fault off
        measure-ready off
    then
;

: measure-T4
    measure-go @ not
    dup if
        measure-S1-done off
    then
;

: measure-T5
    measure-go @
    dup if
        measure-S3-done off
    then
;

: measure-T6
    \ TODO: 暫時不判斷 AC20V 狀態
    \ AC20V-on? 0<>
    false
    dup if
        measure-in-setting on
        measure-fault on
    then
;

: measure-T7
    \ TODO: 暫時不判斷 AC20V 狀態
    \ AC20V-on? not
    false
    dup if
        measure-in-setting on
        measure-fault on
    then
;

step measure-S0
step measure-S1
step measure-S2
step measure-S3

transition measure-T0
transition measure-T1
transition measure-T2
transition measure-T3
transition measure-T4
transition measure-T5
transition measure-T6
transition measure-T7

' measure-S0 ' measure-T0 -->
' measure-T0 ' measure-S1 -->
' measure-S1 ' measure-T1 -->
' measure-T1 ' measure-S2 -->
' measure-S2 ' measure-T2 -->
' measure-T2 ' measure-S3 -->
' measure-S3 ' measure-T3 -->
' measure-T3 ' measure-S0 -->

' measure-S1 ' measure-T4 -->
' measure-T4 ' measure-S3 -->

' measure-S3 ' measure-T5 -->
' measure-T5 ' measure-S1 -->

\ TODO: 暫時關閉
\ ' measure-S0 ' measure-T6 -->
\ ' measure-T6 ' measure-S3 -->

\ ' measure-S2 ' measure-T7 -->
\ ' measure-T7 ' measure-S1 -->



\ ==================== motion state ====================
\ 說明:
\   啟動後會在 motion-state-serving 處理 motion 狀態機和每個週期都要進行的工作
\
\ SFC:
\           motion-state-init
\                   |
\                   + motion-state-init-done?
\                   |
\                   v
\          motion-state-serving
\

variable motion-state-init-done

\ -------------------- steps --------------------
\ 初始化
: motion-state-init ( -- )
    measure-go on                   \ 開啟量測電路
    0 42 RMT-led!                   \ 關閉遙控器 short LED

    motion-state-init-done on
;
step motion-state-init

\ 處理 motion-state 狀態機，及 cnc ready 後每個週期都要進行的工作
: motion-state-serving ( -- )
    motion-state-ffs-forth          \ 處理正反器
    motion-state-forth              \ 更新 motion state
;
step motion-state-serving

\ -------------------- transitions --------------------
\ 初始化完成？
: motion-state-init-done? ( -- flag )
    motion-state-init-done @
;
transition motion-state-init-done?

\ -------------------- links --------------------
' motion-state-init ' motion-state-init-done? --> ' motion-state-init-done? ' motion-state-serving -->


marker -work