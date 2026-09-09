-work

: @system-error ( -- )
    EMS-din ec-din@ not system-EMS !
    3phase-din ec-din@ not system-3phase-error !
    sol3-level-din ec-din@ sol3-level-error !
    cr6-level f@ cr6-level-LB f@ f< cr6-level-error !
;

: system-error? ( -- flag )
    system-EMS @
    system-3phase-error @ or
    sol3-level-error @ or
    cr6-level-error @ or
;

: .system-error ( -- )
    system-EMS @
    if
        ." error|In EMS." cr
    then
    system-3phase-error @
    if
        ." error|3phase error." cr
    then
    sol3-level-error @
    if
        ." error|FeSO4 solution low level." cr
    then
    cr6-level-error @
    if
        ." error|Collection tank low level." cr
    then
;



\ ========== steps ==========
variable clocks-inited
variable EC-waiting-clock
variable system-waiting-clock

\ 等待 ec-ready
: wait-EtherCAT-ready ( -- )
    clocks-inited @ not
    if
        EC-waiting-clock renew
        true clocks-inited !
    then

    mtime EC-waiting-clock @ - abs 60000 >
    if
        ." log|EtherCAT not ready." cr
        EC-waiting-clock renew
    then
    system-waiting-clock renew
;
step wait-EtherCAT-ready

\ system 初始化，啟動相關 SFC
: system-init ( -- )
    ['] communication-init +step
    ['] sampling-loop +step
    ['] process-init +step
;
step system-init

\ 初始化完成後或發生異警後，在此狀態等待 system ready 或異警排除
: wait-system-ready ( -- )
    +lamp-red
    false system-ready !

    @system-error
    mtime system-waiting-clock @ - abs 60000 >
    if
        ." log|System not ready." cr
        .system-error
        system-waiting-clock renew
    then
;
step wait-system-ready

\ system ready 後在此狀態監控相關異警
: system-serving ( -- )
    system-ready @ not
    if
        +lamp-yellow
    then
    true system-ready !

    @system-error
    system-error?
    if
        .system-error
    then
    aqua-state
    system-waiting-clock renew
;
step system-serving



\ ========== transitions ==========

\ 於等待中判斷 ec-ready?
: EtherCAT-ready? ( -- flag )
    ec-ready?
;
transition EtherCAT-ready?

\ system 初始化完成，轉移至下個狀態
: system-init-end? ( -- flag )
    true
;
transition system-init-end?

\ 於等待中判斷是否可以進入 serving
: system-ready-to-serve? ( -- flag )
    system-error? not
    ['] wait-system-ready elapsed 10000 > and
;
transition system-ready-to-serve?

\ 於 serving 狀態中判斷是否發生異常且需切到 wait-system-ready
: system-not-ready? ( -- flag )
    system-error?
;
transition system-not-ready?



\ =============== SFC ===============
\
\         wait-EtherCAT-ready
\                  |
\                  + EtherCAT-ready?
\                  |
\                  v
\             system-init
\                  |
\                  + system-init-end?
\                  |
\                  v
\          wait-system-ready
\                  |
\                  + system-ready-to-serve?
\                  |
\                  v
\           system-serving
\                  |
\                  +------------------------+
\                  |                        |
\                  + system-not-ready?      + system-off-requested? (TODO)
\                  |                        |
\                  v                        v
\          wait-system-ready            system-off (TODO)
\

' wait-EtherCAT-ready   ' EtherCAT-ready?           -->     ' EtherCAT-ready?           ' system-init       -->
' system-init           ' system-init-end?          -->     ' system-init-end?          ' wait-system-ready -->
' wait-system-ready     ' system-ready-to-serve?    -->     ' system-ready-to-serve?    ' system-serving    -->
' system-serving        ' system-not-ready?         -->     ' system-not-ready?         ' wait-system-ready -->

' wait-EtherCAT-ready +step



: .status
    ." ec-ready?|" ec-ready? . cr
    ." system-ready|" system-ready @ . cr
    ." system-EMS|" system-EMS @ . cr
    ." system-3phase-error|" system-3phase-error @ . cr
    ." sol3-level-error|" sol3-level-error @ . cr
    ." communication-fault|" communication-fault @ . cr
    ." pH-balance-time|" mtime pH-balance-clock @ - . cr
    ." pH-pre-state|" pH-pre-state @ . cr
    ." collection-tank-level-UB|" cr6-level f@ cr6-level-UB f@ f> . cr
    ." pre-pH-OB?|" pre-pH-OB? . cr
    ." cr6-state|" cr6-state @ . cr
    ." cr6-concent-setted|" cr6-concent-setted @ . cr
    ." sol3-concent-setted|" sol3-concent-setted @ . cr
    ." params-lock|" params-lock @ . cr
    ." cr6-stroke-number|" cr6-stroke-number @ . cr
    ." m3-stroke-counter|" m3-stroke-counter @ . cr
    ." pH-post-state|" pH-post-state @ . cr
    ." post-pH-OB?|" post-pH-OB? . cr
    ." collection-tank-level-LB|" cr6-level f@ cr6-level-LB f@ f< . cr
;

marker -work