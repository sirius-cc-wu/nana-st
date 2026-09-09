-work

\ ========== configurations ==========
variable pH-pre-start-req
variable pH-pre-stop-req
variable pH-post-start-req
variable pH-post-stop-req
variable cr6-start-req
variable cr6-stop-req

variable pH-balance-clock
variable cr6-stroke-number
variable m3-stroke-counter
variable m3-stroke-clock
variable m3-stroke-period

variable cr6-concent-setted
variable sol3-concent-setted true sol3-concent-setted !
variable params-lock

fvariable m3-spm
fvariable cr6-concent
fvariable sol3-concent
fvariable factor

0.2e sol3-concent f!


: m3-spm! ( F: spm -- )
    fdup 1e f< fdup 100e f> or
    if
        fdrop
        ." error|M3 SPM must >1 and <100." cr
    else
        fdup m3-spm f!
        60000e fswap f/ f>s m3-stroke-period !
    then
;
60e m3-spm!

: m3-on ( -- )
    m3-spm f@ 100e f/
    2e 15e f** 1e f- f*
    f>s m3-spm-aout ec-aout!
;

: m3-off ( -- )
    0 m3-spm-aout ec-aout!
;

: cr6-concent! ( F: concent -- )
    params-lock @
    if
        fdrop
        ." error|System processing. Parameter changing unaccepted." cr
    else
        cr6-concent f!
        true cr6-concent-setted !
    then
;

: sol3-concent! ( F: concent -- )
    params-lock @
    if
        fdrop
        ." error|System processing. Parameter changing unaccepted." cr
    else
        sol3-concent f!
        true sol3-concent-setted !
    then
;

: factor! ( F: f -- )
    params-lock @
    if
        fdrop
        ." error|System processing. Parameter changing unaccepted." cr
    else
        factor f!
    then
;
1e factor!

: aqua-state
    cr6-level f@ cr6-level-UB f@ f> if
        cr6-level-high-dout on
    else
        cr6-level-high-dout off
    then

    cr6-level f@ cr6-level-LB f@ f> if
        cr6-level-low-dout on
        -buzzer
    else
        cr6-level-low-dout off
        +buzzer
    then
    
    system-EMS @ not if
        cr6-onReady-dout on
    else
        cr6-onReady-dout off
    then
;

\ ========== pH pre-process commands ==========
: +pH-pre-allow? ( -- flag )
    system-ready @                              \ system ready
    dup not if
        ." error|System not ready." cr
        exit
    then

    pH-pre-state @ 1 =
    pH-pre-state @ 3 = or and                   \ 且 pH pre-process ready
    dup not if
        ." error|pH pre-process not ready or in processing." cr
        exit
    then

    cr6-level f@ cr6-level-UB f@ f> and         \ 且水位已達
    dup not if
        ." error|Collection tank level not reached." cr
        exit
    then
;

: +pH-pre ( -- )
    +pH-pre-allow?
    if
        true pH-pre-start-req !
    then
;

: -pH-pre-allow? ( -- flag )
    pH-pre-state @ 1 =                          \ PH pre-processing
    dup not if
        ." error|Not in pH pre-processing." cr
        exit
    then
;

: -pH-pre ( -- )
    -pH-pre-allow?
    if
        true pH-pre-stop-req !
    then
;

: pre-pH-OB? ( -- flag )
    cr6-pH f@ fdup pre-pH-UB f@ f> pre-pH-LB f@ f< or
;

: pH-pre-start ( -- )
    pH-balance-clock renew
    m4-op-dout on
    LDPH-op-dout on
    +lamp-green
;

: pH-pre-stop ( -- )
    m4-op-dout off
    LDPH-op-dout off
    +lamp-yellow
;



\ ========== cr6 process commands ==========
: +cr6-allow? ( -- flag )
    system-ready @                              \ system ready
    dup not if
        ." error|System not ready." cr
        exit
    then

    pH-pre-state @ 3 =
    cr6-state @ 1 = or
    cr6-state @ 3 = or and                      \ 且 cr6 process ready
    dup not if
        ." error|Cr6 process not ready or in processing." cr
        exit
    then

    cr6-concent-setted @
    sol3-concent-setted @ and and               \ 且參數已設定
    dup not if
        ." error|Cr6 parameters not setted." cr
        exit
    then
;

: +cr6 ( -- )
    +cr6-allow?
    if
        \ 計算投藥量
        7.6e cr6-concent f@ f*
        cr6-L@ f*
        factor f@ f*
        0.001e f*
        fdup 1.89e f/ fswap 
        1e sol3-concent f@ f/ 1e f-
        f* f+
        \ 假設一次衝程 1.66 毫升
        1.66e f/ f>s 1 + cr6-stroke-number !

        true params-lock !
        true cr6-start-req !
    then
;

: -cr6-allow? ( -- flag )
    cr6-state @ 2 =                             \ cr6 processing
    dup not if
        ." error|Not in Cr6 processing." cr
        exit
    then
;

: -cr6 ( -- )
    -cr6-allow?
    if
        true cr6-stop-req !
    then
;

: 0cr6-allow? ( -- )
    cr6-state @ 2 = not                         \ Not cr6 processing
    dup not if
        ." error|Cr6 processing. Reset unaccepted." cr
        exit
    then
;

: 0cr6 ( -- )
    0cr6-allow?
    if
        \ false cr6-concent-setted !
        \ false sol3-concent-setted !
        false params-lock !
        0 m3-stroke-counter !
    then
;

: cr6-start ( -- )
    m3-on
    m3-stroke-clock renew
    m4-op-dout on
    +lamp-green
;

: cr6-stop ( -- )
    m3-off
    m4-op-dout off
    +lamp-yellow
;



\ ========== pH post-process commands ==========
: +pH-post-allow? ( -- flag )
    system-ready @                              \ system ready
    dup not if
        ." error|System not ready." cr
        exit
    then

    cr6-state @ 3 =
    pH-post-state @ 1 = or
    pH-post-state @ 3 = or and                  \ 且 pH post-process ready
    dup not if
        ." error|pH post-process not ready or in processing." cr
        exit
    then
;

: +pH-post ( -- )
    +pH-post-allow?
    if
        true pH-post-start-req !
    then
;

: -pH-post-allow? ( -- flag )
    pH-post-state @ 1 =                         \ pH post-processing
    dup not if
        ." error|Not in pH post-processing." cr
        exit
    then
;

: -pH-post ( -- )
    -pH-post-allow?
    if
        true pH-post-stop-req !
    then
;

: post-pH-OB? ( -- flag )
    cr6-pH f@ fdup post-pH-UB f@ f> post-pH-LB f@ f< or
;

: pH-post-start ( -- )
    pH-balance-clock renew
    m4-op-dout on
    LDPH-op-dout on
    +lamp-green
;

: pH-post-stop ( -- )
    m4-op-dout off
    LDPH-op-dout off
    +lamp-yellow
;



\ =============== steps ===============
variable process-init-done
variable pH-pre-done
variable cr6-done
variable pH-post-done

: process-init
    10e m4-rpm!
    true process-init-done !
;
step process-init

: pH-pre-init
    1 pH-pre-state !
    false pH-pre-done !
;
step pH-pre-init

: pH-pre-forth
    pH-pre-state @
    case
        \ idle
        1 of
            \ TODO 改為接受到人機要求才啟動？
            \ pH-pre-start-req accepted?
            system-ready @
            cr6-level f@ cr6-level-UB f@ f> and
            if
                pH-pre-start
                2 pH-pre-state !
            then
        endof

        \ processing
        2 of
            system-ready @                                  \ system-ready
            \ TODO 改為可以接受人機停止命令？
            \ pH-pre-stop-req accepted? not and             \ 且沒有收到 stop request
            if
                \ 若 pH 值在區間外就更新碼錶
                pre-pH-OB?
                if
                    pH-balance-clock renew
                then

                \ 若已平衡就切換到 finished
                mtime pH-balance-clock @ - abs pH-balance-cycle @ >
                if
                    pH-pre-stop
                    3 pH-pre-state !
                    ." log| 酸鹼中和完畢。 "
                    \ +buzzer
                then
            else
                \ 切換到 idle
                pH-pre-stop
                1 pH-pre-state !
            then
        endof

        \ finished
        3 of
            \ TODO 改為可以接受人機命令重新處理？
            \ pH-pre-start-req accepted?                    \ 收到 start request
            false
            if
                \ 切換到 processing 重新處理
                pH-pre-start
                2 pH-pre-state !
                -buzzer
            else
                cr6-start-req accepted?                     \ cr6 start requested
                if
                    -buzzer
                    0 pH-pre-state !
                    true pH-pre-done !
                then
            then
        endof
    endcase
;
step pH-pre-forth

: cr6-init
    true cr6-start-req !
    1 cr6-state !
    false cr6-done !
;
step cr6-init

: cr6-forth
    cr6-state @
    case
        \ idle
        1 of
            cr6-start-req accepted?                         \ 且 cr6 start requested
            if
                \ 切換到 processing
                cr6-start
                2 cr6-state !
                ." log| 加入硫酸亞鐵作業中。 "
            then
        endof

        \ processing
        2 of
            system-ready @                                  \ system ready
            cr6-stop-req accepted? not and                  \ 且沒有 cr6 stop request
            if
                mtime m3-stroke-clock @ - abs m3-stroke-period @ >
                if
                    m3-stroke-counter @ cr6-stroke-number @ <
                    if
                        1 m3-stroke-counter +!
                        m3-stroke-clock renew
                    else
                        \ 切換到 finished
                        cr6-stop
                        3 cr6-state !
                        0cr6
                        \ +buzzer
                    then
                then
            else
                \ 切換到 idle
                cr6-stop
                1 cr6-state !
            then
        endof

        \ finished
        3 of
            \ TODO 改為可以接受人機開始命令重新處理？
            \ cr6-start-req accepted?                       \ 收到 start request
            false
            if
                \ 切換到 processing 重新處理
                cr6-start
                2 cr6-state !
                \ -buzzer
            else
                \ TODO 改為可以接收人機命令才開始 post process?
                \ pH-post-start-req accepted?                 \ pH post start requested
                true
                if
                    0 cr6-state !
                    \ -buzzer
                    true cr6-done !
                    ." log| 加入硫酸亞鐵作業完畢。 "
                then
            then
        endof
    endcase
;
step cr6-forth

: pH-post-init
    1 pH-post-state !
    false pH-post-done !
;
step pH-post-init

: pH-post-forth
    pH-post-state @
    case
        \ idle
        1 of
            \ TODO 改為接受到人機要求才啟動？
            \ pH-post-start-req accepted?
            system-ready @
            if
                pH-post-start
                2 pH-post-state !
            then
        endof

        \ processing
        2 of
            system-ready @                                  \ system-ready
            \ TODO 改為可以接受人機停止命令？
            \ pH-post-stop-req accepted? not and            \ 且沒有收到 stop request
            if
                \ 若 pH 值在區間外就更新碼錶
                \ post-pH-OB?
                \ if
                \     pH-balance-clock renew
                \ then

                \ 若已平衡就切換到 finished
                \ mtime pH-balance-clock @ - abs pH-balance-cycle @ >
                \ 後處理目前不等待
                \ if
                    pH-post-stop
                    3 pH-post-state !
                    \ +buzzer
                \ then
            else
                \ 切換到 idle
                pH-post-stop
                1 pH-post-state !
            then
        endof

        \ finished
        3 of
            \ TODO 改為可以接受人機命令重新處理？
            \ pH-post-start-req accepted?                   \ 收到 start request
            false
            if
                \ 切換到 processing 重新處理
                pH-post-start
                2 pH-post-state !
                \ -buzzer
            else
                \ cr6-level f@ cr6-level-LB f@ f<             \ 水位已下降
                \ if
                    \ -buzzer
                    0 pH-post-state !
                    true pH-post-done !
                \ then
            then
        endof
    endcase
;
step pH-post-forth



\ =============== transitions ===============
: process-init-done? ( -- flag )
    process-init-done @
;
transition process-init-done?

: pH-pre-init-end? ( -- flag )
    true
;
transition pH-pre-init-end?

: pH-pre-done? ( -- flag )
    pH-pre-done @
;
transition pH-pre-done?

: cr6-init-end? ( -- flag )
    true
;
transition cr6-init-end?

: cr6-done? ( -- flag )
    cr6-done @
;
transition cr6-done?

: pH-post-init-end? ( -- flag )
    true
;
transition pH-post-init-end?

: pH-post-done? ( -- flag )
    pH-post-done @
;
transition pH-post-done?



\ =============== SFC ===============
\
\             process-init
\                  |
\                  + process-init-done?
\                  |
\                  v
\            pH-pre-init
\                  |
\                  + pH-pre-init-end?
\                  |
\                  v
\            pH-pre-forth
\                  |
\                  + pH-pre-done?
\                  |
\                  v
\              cr6-init
\                  |
\                  + cr6-init-end?
\                  |
\                  v
\             cr6-forth
\                  |
\                  + cr6-done?
\                  |
\                  v
\            pH-post-init
\                  |
\                  + pH-post-init-end?
\                  |
\                  v
\           pH-post-forth
\                  |
\                  + pH-post-done?
\                  |
\                  v
\            pH-pre-init
\

' process-init  ' process-init-done?    --> ' process-init-done?    ' pH-pre-init   -->
' pH-pre-init   ' pH-pre-init-end?      --> ' pH-pre-init-end?      ' pH-pre-forth  -->
' pH-pre-forth  ' pH-pre-done?          --> ' pH-pre-done?          ' cr6-init      -->
' cr6-init      ' cr6-init-end?         --> ' cr6-init-end?         ' cr6-forth     -->
' cr6-forth     ' cr6-done?             --> ' cr6-done?             ' pH-post-init  -->
' pH-post-init  ' pH-post-init-end?     --> ' pH-post-init-end?     ' pH-post-forth -->
' pH-post-forth ' pH-post-done?         --> ' pH-post-done?         ' pH-pre-init   -->



marker -work