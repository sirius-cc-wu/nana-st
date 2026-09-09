-work

\ ==================== trace ====================
\ 說明:
\   1. 外部可以呼叫 trace-to-start/trace-to-hold 命令, 若呼叫成功即開始 trace 程序
\   2. 可以使用 stop-motion 命令暫停 trace, 暫停後再次呼叫 trace-to-start/trace-to-hold 即可繼續
\   3. trace 中由 MS-forth 進行異常檢查, 若發生異警即放棄該次 trace 並回到暫停狀態,
\      異警排除後可以再次呼叫 trace-to-start/trace-to-hold 即可繼續
\
\ 位置紀錄時機:
\   1. 加工（也有可能是空跑）暫停時要紀錄 hold-position （要紀錄 control point 所需要的資料）與 tracing-position（紀錄機械座標）。
\   2. tracing 停止時，如果是停在路徑 P 上，要紀錄 tracing-position（紀錄機械座標）。
\
\ FeedHold 狀態下繼續加工的條件:
\   1. 目前位置在 tracing-position 上就可以繼續加工。
\
\ FeedHold 狀態下要退到路徑入口點:
\   1. 目前位置在 tracing-position 上就可以循路徑退到 start-position。
\   2. 到達 start-position 或是使用者要求停止，就要停止 tracing。停止後要紀錄 tracing-position。
\
\ FeedHold 狀態下要循路徑回到加工停止點:
\   1. 先使用軸移動回到 tracing-position，再依循路徑回到 hold-position。
\   2. 到達 hold-position 或是使用者要求停止，就要停止 tracing。如果是在 tracing-position 到 hold-position 的階段就要紀錄 tracing position。
\
\ Resume 操作案例:
\   1. 加工停止後就用軸移動移開。（trace -> resume）
\   2. 加工停止後，使用 retrace，停在路徑上。 (trace -> resume 或是 resume)
\   3. 加工停止後，使用 retrace，停在路徑上，再使用軸移動移開。（trace -> resume）
\   4. trace 時停在路徑上。（trace ->resume 或是 resume）
\   5. trace 時沒有停在路徑上。（trace ->resume）
\
\ 操作案例:
\
\   ================================== 加工中 ==================================
\                     |                   ^                            ^
\                     |                   |                            |
\               stop-motion        restart-motion                      |
\                     |                   |                            |
\                     v                   |                            |
\   ======================= 加工停止點 =======================     restart-motion
\        |          ^                  |               ^               |
\        |          |                  |               |               |
\        |          |           trace-to-start   trace-to-hold         |
\        |          |                  |               |               |
\        |          |                  v               |               |
\       jog   trace-to-hold    =================== 軸組路徑上 ===================
\        |          |                             |         ^
\        |          |                             |         |
\        |          |                            jog  trace-to-hold
\        |          |                             |         |
\        v          |                             v         |
\   ====== 軸組路徑外 ======                  ====== 軸組路徑外 ======
\
\
\ SFC:
\               trace-init
\                   |
\                   + trace-init-done?
\                   |
\                   v
\               trace-idle
\                   |
\                   +-------------------------------+
\                   |                               |
\                   + trace-to-start?               + trace-to-hold?
\                   |                               |
\                   v                               v
\         trace-to-start-forth             trace-to-hold-forth
\                   |                               |
\                   + trace-to-start-end?           + trace-to-hold-end?
\                   |                               |
\                   +-------------------------------+
\                   |
\                   v
\               trace-init
\

1 constant to-start
2 constant to-hold

variable trace-init-done
variable trace-in-idle
variable trace-cmd
variable trace-step

\ 回傳軸組路徑是否已回到 hold position
: path-on-hold-p? ( -- flag )
    1 group! next-path-p@ path-hold-p f@ 0.5e um trace-to-hold-margin f@ f+ f- f>
;

\ 回傳 tracing SFC 是否 idle
: trace-in-idle? ( -- flag )
    trace-in-idle @
;

\ X,Y 軸以 tracing rate 返回 tracing position
: XY-to-tracing-p ( -- )
    PECM-XA-axis
    dup tracing-rate@ interpolator-v!
    dup +interpolator
    X-tracing-p f@ axis-cmd-p!

    PECM-XB-axis
    dup tracing-rate@ interpolator-v!
    dup +interpolator
    Y-tracing-p f@ axis-cmd-p!
;

\ Z 軸以 tracing rate 返回 tracing position
: Z-to-tracing-p ( -- )
    PECM-XC-axis
    dup tracing-rate@ interpolator-v!
    dup +interpolator
    Z-tracing-p f@ axis-cmd-p!
;

\ 回傳是否可以進行 trace-to-start
: trace-to-start-allowed? ( -- flag )
    feedhold?
    if                                  \ feedhold 中
        machining-mode @ dup nc =
        swap trace = or                     \ machining mode 為 nc 或 trace
        at-tracing-p? and                   \ 且 X,Y,Z 軸在 tracing position
    else
        false
    then

    1 group! gbeginning? not and        \ 且不在路徑起點
    trace-in-idle? and                  \ 且 tracing SFC in idle
    PECM-power-state @ and               \ 且 power on 中
    PECM-on-sl? not and                  \ 且未碰觸軟體極限
    is-touch? not and                   \ 且未發生碰邊
;

\ 印出導致 trace-to-start 失敗的原因
: .trace-to-start-errors ( -- )
    feedhold?
    if
        machining-mode @ dup nc = swap trace = or not
        if
            ." error|Not in NC or trace mode. Trace to start point failed. ;A1547" cr
        then
        at-tracing-p? not
        if
            ." error|Jogged axis not returned to tracing point. Trace to start point failed. ;A1548" cr
        then
    else
        ." error|Not in feedhold. Trace to start point failed. ;A1549" cr
    then
    1 group! gbeginning?
    if
        ." error|Already on start point of axis group path. Trace to start point failed. ;A1550" cr
    then
    trace-in-idle? not
    if
        ." error|Trace in busy. Trace to start point failed. ;A1551" cr
    then
    PECM-power-state @ not
    if
        ." error|Not power on. Trace to start point failed. ;A1552" cr
    then
    PECM-on-sl?
    if
        ." error|On software limit. Trace to start point failed. ;A1553" cr
    then
    is-touch?
    if
        measure-on?
        if
            ." error|In touch. Trace to start point failed. ;A1554" cr
        else
            ." error|Short protection circuit not enabled. Trace to start point failed. ;A1555" cr
        then
    then
;

\ 沿路徑退出至 start position
: trace-to-start ( -- )
    trace-to-start-allowed?
    if
        machining PECM-motion-state !        \ 設定為 machining
        trace machining-mode !              \ 設定為 trace
        serving motion-status !             \ 設定為 serving
        1 20 RMT-led!                       \ 開啟遙控器拉起入口按鍵 LED
        trace-in-idle off
        to-start trace-cmd !                \ 設定為 to-start
        ." log|Start tracing to start point." cr
    else
        .trace-to-start-errors
    then
;

\ 回傳是否可以進行 trace-to-hold
: trace-to-hold-allowed? ( -- flag )
    feedhold?
    if                                  \ feedhold 中
        at-tracing-p? not                       \ X,Y,Z 軸不在 tracing position
        path-on-hold-p? not or                  \ 或軸組未回到 hold position

        machining-mode @ trace = and            \ 且 machining mode 為 trace
    else
        false
    then

    trace-in-idle? and                  \ 且 tracing SFC in idle
    PECM-power-state @ and               \ 且 power on 中
    PECM-on-sl? not and                  \ 且未碰觸軟體極限
    is-touch? not and                   \ 且未發生碰邊
;

\ 印出導致 trace-to-hold 失敗的原因
: .trace-to-hold-errors ( -- )
    feedhold?
    if
        at-tracing-p? path-on-hold-p? and
        machining-mode @ trace <> or
        if
            ." error|Axis group already on hold point. Trace to hold point failed. ;A1556" cr
        then
    else
        ." error|Not in feedhold. Trace to hold point failed. ;A1557" cr
    then
    trace-in-idle? not
    if
        ." error|Trace in busy. Trace to hold point failed. ;A1558" cr
    then
    PECM-power-state @ not
    if
        ." error|Not power on. Trace to hold point failed. ;A1559" cr
    then
    PECM-on-sl?
    if
        ." error|On software limit. Trace to hold point failed. ;A1560" cr
    then
    is-touch?
    if
        measure-on?
        if
            ." error|In touch. Trace to hold point failed. ;A1561" cr
        else
            ." error|Short protection circuit not enabled. Trace to hold point failed. ;A1562" cr
        then
    then
;

\ 返回 tracing position 和沿路徑返回 hold position
: trace-to-hold ( -- )
    trace-to-hold-allowed?
    if
        machining PECM-motion-state !        \ 設定為 machining
        serving motion-status !             \ 設定為 serving
        1 19 RMT-led!                       \ 開啟遙控器返回入口按鍵 LED
        trace-in-idle off
        to-hold trace-cmd !                 \ 設定為 to-hold
        ." log|Start tracing to hold point." cr
    else
        .trace-to-hold-errors
    then
;



\ ==================== steps ====================
\ 初始化
: trace-init ( -- )
    0 trace-step !
    0 19 RMT-led!                   \ 關閉遙控器返回入口按鍵 LED
    0 20 RMT-led!                   \ 關閉遙控器拉起入口按鍵 LED
    trace-in-idle on
    trace-init-done on
;
step trace-init

\ 等待中
: trace-idle ( -- )
    ( do nothing )
;
step trace-idle

\ 處理 trace to start 工作
: trace-to-start-forth ( -- )
    motion-status @ serving =       \ 若 motion status 為 serving
    if
        trace-step @
        case
            \ 處理沿路徑退出
            0 of
                1 group! tracing-rate@ -1e f* vcmd! \ 設定 vcmd 為負
                1 group! +group gstart              \ 啟動 group1
                1 trace-step !                      \ 切換至等待沿路徑退出完成
            endof

            \ 等待沿路徑退出完成並離開 tracing
            1 of
                job-stop?
                if
                    !tracing-p                          \ 紀錄軸組路徑停留的 acs position
                    stopping motion-status max!         \ 切換到 stopping
                    trace-cmd off                       \ 清除 trace command, 離開 trace-to-start-forth
                then
            endof
        endcase
    else                            \ 若 motion status 離開 serving 表示可能發生異警, 放棄 trace-to-start
        job-stop?
        if
            !tracing-p                      \ 紀錄軸組路徑停留的 acs position
            trace-cmd off                   \ 清除 trace command, 離開 trace-to-start-forth
        then
    then
;
step trace-to-start-forth

\ 處理 trace to hold 工作
: trace-to-hold-forth ( -- )
    motion-status @ serving =       \ 若 motion status 為 serving
    if
        trace-step @
        case
            \ 處理第一階段返回 tracing position
            0 of
                at-tracing-p?               \ 若 X,Y,Z 軸已在 tracing position
                if
                    4 trace-step !              \ 切換至處理沿路徑返回 hold position
                else
                    PECM-XC-axis axis-cmd-p@ Z-tracing-p f@ f<     \ 判斷若 Z 軸需往上返回，就先返回 Z 軸
                    if
                        Z-to-tracing-p              \ Z 軸返回 tracing position
                    else
                        XY-to-tracing-p             \ X,Y 軸返回 tracing position
                    then
                    1 trace-step !              \ 切換到等待第一階段返回 tracing position 完成
                then
            endof

            \ 等待第一階段返回 tracing position 完成
            1 of
                job-stop?
                if
                    stop-job
                    2 trace-step !              \ 切換到處理第二階段返回 tracing position
                then
            endof

            \ 處理第二階段返回 tracing position
            2 of
                at-tracing-p?               \ 若 X,Y,Z 軸已在 tracing position
                if
                    4 trace-step !              \ 切換至處理沿路徑返回 hold position
                else
                    PECM-XC-axis axis-cmd-p@ Z-tracing-p f@ f- fabs 0.5e um f<    \ 若 Z 軸已返回 tracing position
                    if
                        XY-to-tracing-p             \ X,Y 軸返回 tracing position
                    else
                        Z-to-tracing-p              \ Z 軸返回 tracing position
                    then
                    3 trace-step !              \ 切換到等待第二階段返回 tracing position 完成
                then
            endof

            \ 等待第二階段返回 tracing position 完成
            3 of
                job-stop?
                if
                    stop-job

                    at-tracing-p?               \ 若 X,Y,Z 軸已在 tracing position
                    if
                        4 trace-step !              \ 切換到處理沿路徑返回 hold position
                    else                        \ 返回 tracing position 失敗
                        stopping motion-status max! \ 切換到 stopping
                        trace-cmd off               \ 清除 trace command, 離開 trace-to-hold-forth
                    then
                then
            endof

            \ 處理沿路徑返回 hold position
            4 of
                path-on-hold-p?                 \ 若軸組路徑已回到 hold position
                if
                    stopping motion-status max!     \ 切換到 stopping
                    trace-cmd off                   \ 清除 trace command, 離開 trace-to-hold-forth
                else
                    1 path-hold-p f@ trace-to-hold-margin f@ f- 0e fmax cp-as-stop  \ 設定 control point
                    1 group! tracing-rate@ vcmd!    \ 設定 vcmd
                    +group gstart                   \ 啟動 group1
                    5 trace-step !                  \ 切換到等待沿路徑返回 hold position 完成
                then
            endof

            \ 等待沿路徑返回 hold position 完成
            5 of
                job-stop?
                if
                    !tracing-p                      \ 紀錄軸組路徑停留的 acs position
                    1 -cp                           \ 關閉 control point
                    stopping motion-status max!     \ 切換到 stopping
                    trace-cmd off                   \ 清除 trace command, 離開 trace-to-hold-forth
                then
            endof
        endcase
    else                            \ 若 motion status 離開 serving 表示可能發生異警, 放棄 trace-to-hold
        job-stop?
        if
            trace-step @ 3 >            \ 若 step > 3 表示正在處理沿路徑返回 hold position，tracing position 已經變了
            if
                !tracing-p                  \ 紀錄軸組路徑停留的 acs position
                1 -cp                       \ 關閉 control point
            then
            trace-cmd off               \ 清除 trace command, 離開 trace-to-hold-forth
        then
    then
;
step trace-to-hold-forth



\ ==================== transitions ====================
\ 初始化完成?
: trace-init-done? ( -- flag )
    trace-init-done @
;
transition trace-init-done?

\ 開始沿路徑退出至 start position?
: trace-to-start? ( -- flag )
    trace-cmd @ to-start =
;
transition trace-to-start?

\ 開始返回 tracing position 和沿路徑返回 hold position?
: trace-to-hold? ( -- flag )
    trace-cmd @ to-hold =
;
transition trace-to-hold?

\ 結束沿路徑退出至 start position?
: trace-to-start-end? ( -- flag )
    trace-cmd @ 0=
;
transition trace-to-start-end?

\ 結束返回 tracing position 和沿路徑返回 hold position?
: trace-to-hold-end? ( -- flag )
    trace-cmd @ 0=
;
transition trace-to-hold-end?



\ ==================== links ====================
' trace-init           ' trace-init-done?     --> ' trace-init-done?     ' trace-idle           -->

' trace-idle           ' trace-to-start?     --> ' trace-to-start?     ' trace-to-start-forth -->
' trace-to-start-forth ' trace-to-start-end? --> ' trace-to-start-end? ' trace-init           -->

' trace-idle           ' trace-to-hold?      --> ' trace-to-hold?      ' trace-to-hold-forth  -->
' trace-to-hold-forth  ' trace-to-hold-end?  --> ' trace-to-hold-end?  ' trace-init           -->



marker -work