-work

\ ==================== run time ====================
\ 說明:
\   紀錄加工時間
\ SFC:
\              run-time-idle
\                   |
\                   + machining?
\                   |
\                   v
\              run-time-init
\                   |
\                   + run-time-ready?
\                   |
\                   v
\             record-run-time
\                   |
\                   + <>machining?
\                   |
\                   v                  
\              stop-run-time
\                   +-----------------------+
\                   |                       |
\                   + re-machining?         + nc-end?
\                   |                       |
\                   v                       v
\              record-run-time          run-time-idle

: run-time-idle     ( -- )
    ( wait ) 
;
step run-time-idle

variable run-time-count
variable short-time-count
variable arc-alarm-count

: run-time-init     ( -- )
    0 run-time-count !
    0 short-time-count !
    0 arc-alarm-count !
;
step run-time-init

: record-run-time   ( -- )
    1 run-time-count +!
    short-alarm-ch-slv ec-din@ not if
        1 short-time-count +!
    then
    cutting-arc? if
        1 arc-alarm-count +!
    then
;
step record-run-time

: stop-run-time     ( -- )
    ( wait ) 
;
step stop-run-time

: run-time-ready?   ( -- flag )
    true
;
transition run-time-ready?

: machining?        ( -- flag )
    PECM-motion-state @ machining =
;
transition machining?

: <>machining?      ( --flag )
    PECM-motion-state @ machining <>
;
transition <>machining?

: re-machining?     ( -- flag )
    PECM-motion-state @ machining =
;
transition re-machining?

: nc-end?             ( -- flag )
    PECM-motion-state @ idle =
    machining-mode @ none = and
;
transition nc-end?

\ ==================== links ====================
' run-time-idle         ' machining?        -->
' machining?            ' run-time-init     -->
' run-time-init         ' run-time-ready?   -->
' run-time-ready?       ' record-run-time   -->
' record-run-time       ' <>machining?      -->
' <>machining?          ' stop-run-time     -->

' stop-run-time         ' re-machining?     -->
' re-machining?         ' record-run-time   -->

' stop-run-time         ' nc-end?             -->
' nc-end?               ' run-time-idle     -->

: .run-time         ( -- )
    ." run-time|" run-time-count @ s>f period-ms@ f* f>s 1000 / . cr
;

: .short-time-count       ( -- )
    ." short-time-count|" short-time-count @ . cr
;

: .arc-alarm-count       ( -- )
    ." arc-alarm-count|" arc-alarm-count @ . cr
;
marker -work