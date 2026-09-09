1 1 2constant gateway-ch-slv

0 constant none

variable munk-cmd-busy



\ mappings
0 constant auto/manual
2 constant pulse-program
3 constant step-data
4 constant err/msg
: munk-map! ( map -- ) 1 0 gateway-ch-slv gateway-out-be! ;
: munk-map@ ( -- map ) 1 0 gateway-ch-slv gateway-out-be@ ;
: munk-acked-map@ ( -- map ) 1 0 gateway-ch-slv gateway-in-be@ ;



\ commands: 0=none, 1=read, 2=write
: munk-cmd! ( cmd -- ) 1 1 gateway-ch-slv gateway-out-be! ;
: munk-cmd@ ( -- cmd ) 1 1 gateway-ch-slv gateway-out-be@ ;
: munk-acked-cmd@ ( -- cmd ) 1 1 gateway-ch-slv gateway-in-be@ ;

: munk-cmd-write ( -- )
    munk-cmd-busy @ not
    if
        1 munk-cmd!
        true munk-cmd-busy !
    then
;

: munk-cmd-read ( -- )
    munk-cmd-busy @ not
    if
        2 munk-cmd!
        true munk-cmd-busy !
    then
;

: munk-cmd-forth ( -- )
    munk-cmd-busy @
    if
        munk-acked-cmd@ none <>
        if
            ." cmd acked!" cr
            0 munk-cmd!
            false munk-cmd-busy !
        then
    then
;



\ controls
$1 constant ready-to-op
$2 constant switch-on
$4 constant manual-mode
$8 constant auto-mode
$10 constant ack-process-end
$100 constant switch-on-dc
$400 constant ack-faults

: munk-control! ( control -- ) 2 2 gateway-ch-slv gateway-out-le! ;
: munk-control@ ( -- control ) 2 2 gateway-ch-slv gateway-out-le@ ;

: +ready-to-op ( -- ) ready-to-op munk-control@ or munk-control! ;
: -ready-to-op ( -- ) ready-to-op invert munk-control@ and munk-control! ;
: +switch-on ( -- ) switch-on munk-control@ or munk-control! ;
: -switch-on ( -- ) switch-on invert munk-control@ and munk-control! ;
: +manual-mode ( -- ) manual-mode munk-control@ or auto-mode invert and munk-control! ;
: +auto-mode ( -- ) auto-mode munk-control@ or manual-mode invert and munk-control! ;
: +ack-process-end ( -- ) ack-process-end munk-control@ or munk-control! ;
: -ack-process-end ( -- ) ack-process-end invert munk-control@ and munk-control! ;
: +switch-on-dc ( -- ) switch-on-dc munk-control@ or munk-control! ;
: -switch-on-dc ( -- ) switch-on-dc invert munk-control@ and munk-control! ;
: +ack-faults ( -- ) ack-faults munk-control@ or munk-control! ;
: -ack-faults ( -- ) ack-faults invert munk-control@ and munk-control! ;



\ statuses
$1 constant rectifier-on
$2 constant an-error
$4 constant in-manual-mode
$8 constant in-auto-mode
$10 constant rectifier-ready-op
$20 constant op-mode-local
$40 constant op-mode-modbus
$80 constant op-mode-profinet
$100 constant op-mode-profibus
$400 constant main-switch-on
$800 constant end-of-process
$1000 constant a-warning
$2000 constant an-info
$8000 constant discharging

: munk-status@ ( -- status ) 2 2 gateway-ch-slv gateway-in-le@ ;

: rectifier-on? ( -- flag ) rectifier-on munk-status@ and 0<> ;
: an-error? ( -- flag ) an-error munk-status@ and 0<> ;
: in-manual-mode? ( -- flag ) in-manual-mode munk-status@ and 0<> ;
: in-auto-mode? ( -- flag ) in-auto-mode munk-status@ and 0<> ;
: rectifier-ready-op? ( -- flag ) rectifier-ready-op munk-status@ and 0<> ;
: op-mode-local? ( -- flag ) op-mode-local munk-status@ and 0<> ;
: op-mode-modbus? ( -- flag ) op-mode-modbus munk-status@ and 0<> ;
: op-mode-profinet? ( -- flag ) op-mode-profinet munk-status@ and 0<> ;
: op-mode-profibus? ( -- flag ) op-mode-profibus munk-status@ and 0<> ;
: main-switch-on? ( -- flag ) main-switch-on munk-status@ and 0<> ;
: end-of-process? ( -- flag ) end-of-process munk-status@ and 0<> ;
: a-warning? ( -- flag ) a-warning munk-status@ and 0<> ;
: an-info? ( -- flag ) an-info munk-status@ and 0<> ;
: discharging? ( -- flag ) discharging munk-status@ and 0<> ;



\ manual operation mode
0 constant pulse-mode
1 constant dc-mode
2 constant trigger-mode

: manual-op-mode! ( mode --) 1 63 gateway-ch-slv gateway-out-be! ;
: manual-op-mode@ ( -- mode ) 1 69 gateway-ch-slv gateway-in-be@ ;



\ parameters
\ set_V = actual_V(V) * 1000, max:20V
: set-v1! ( v -- ) 4 6 gateway-ch-slv gateway-out-be! ;
: set-v1@ ( -- v ) 4 6 gateway-ch-slv gateway-out-be@ ;
: set-v2! ( v -- ) 4 10 gateway-ch-slv gateway-out-be! ;
: set-v2@ ( -- v ) 4 10 gateway-ch-slv gateway-out-be@ ;
: set-v3! ( v -- ) 4 14 gateway-ch-slv gateway-out-be! ;
: set-v3@ ( -- v ) 4 14 gateway-ch-slv gateway-out-be@ ;
: set-v4! ( v -- ) 4 18 gateway-ch-slv gateway-out-be! ;
: set-v4@ ( -- v ) 4 18 gateway-ch-slv gateway-out-be@ ;
: act-v1@ ( -- v ) 4 15 gateway-ch-slv gateway-in-be@ ;
: act-v2@ ( -- v ) 4 19 gateway-ch-slv gateway-in-be@ ;
: act-v3@ ( -- v ) 4 23 gateway-ch-slv gateway-in-be@ ;
: act-v4@ ( -- v ) 4 27 gateway-ch-slv gateway-in-be@ ;

\ set_I = actual_I(kA) * 1000, max:20kA
: set-a1! ( a -- ) 4 22 gateway-ch-slv gateway-out-be! ;
: set-a1@ ( -- a ) 4 22 gateway-ch-slv gateway-out-be@ ;
: set-a2! ( a -- ) 4 26 gateway-ch-slv gateway-out-be! ;
: set-a2@ ( -- a ) 4 26 gateway-ch-slv gateway-out-be@ ;
: set-a3! ( a -- ) 4 30 gateway-ch-slv gateway-out-be! ;
: set-a3@ ( -- a ) 4 30 gateway-ch-slv gateway-out-be@ ;
: set-a4! ( a -- ) 4 34 gateway-ch-slv gateway-out-be! ;
: set-a4@ ( -- a ) 4 34 gateway-ch-slv gateway-out-be@ ;
: act-a1@ ( -- a ) 4 31 gateway-ch-slv gateway-in-be@ ;
: act-a2@ ( -- a ) 4 35 gateway-ch-slv gateway-in-be@ ;
: act-a3@ ( -- a ) 4 39 gateway-ch-slv gateway-in-be@ ;
: act-a4@ ( -- a ) 4 43 gateway-ch-slv gateway-in-be@ ;

\ set_T = actual_T(msec) * 1000
: set-t1! ( t -- ) 4 38 gateway-ch-slv gateway-out-be! ;
: set-t1@ ( -- t ) 4 38 gateway-ch-slv gateway-out-be@ ;
: set-t2! ( t -- ) 4 42 gateway-ch-slv gateway-out-be! ;
: set-t2@ ( -- t ) 4 42 gateway-ch-slv gateway-out-be@ ;
: set-t3! ( t -- ) 4 46 gateway-ch-slv gateway-out-be! ;
: set-t3@ ( -- t ) 4 46 gateway-ch-slv gateway-out-be@ ;
: set-t4! ( t -- ) 4 50 gateway-ch-slv gateway-out-be! ;
: set-t4@ ( -- t ) 4 50 gateway-ch-slv gateway-out-be@ ;
: act-t1@ ( -- t ) 4 47 gateway-ch-slv gateway-in-be@ ;
: act-t2@ ( -- t ) 4 51 gateway-ch-slv gateway-in-be@ ;
: act-t3@ ( -- t ) 4 55 gateway-ch-slv gateway-in-be@ ;
: act-t4@ ( -- t ) 4 59 gateway-ch-slv gateway-in-be@ ;

\ set_sec = actual_sec (sec)
: ramp-sec! ( sec -- ) 2 54 gateway-ch-slv gateway-out-be! ;
: ramp-sec@ ( -- sec ) 2 54 gateway-ch-slv gateway-out-be@ ;
: act-ramp-sec@ ( -- sec ) 2 63 gateway-ch-slv gateway-in-be@ ;
\ set_% = actual_% (%)
: ramp-start-perc! ( percent -- ) 2 56 gateway-ch-slv gateway-out-be! ;
: ramp-start-perc@ ( -- percent ) 2 56 gateway-ch-slv gateway-out-be@ ;
: act-ramp-start-perc@ ( -- percent ) 2 65 gateway-ch-slv gateway-in-be@ ;
\ set_% = actual_% (%)
: ramp-final-perc! ( percent -- ) 2 58 gateway-ch-slv gateway-out-be! ;
: ramp-final-perc@ ( -- percent ) 2 58 gateway-ch-slv gateway-out-be@ ;
: act-ramp-final-perc@ ( -- percent ) 2 67 gateway-ch-slv gateway-in-be@ ;

\ set_h = actual_h (hour), max:10
: proc-time-h! ( h -- ) 1 60 gateway-ch-slv gateway-out-be! ;
: proc-time-h@ ( -- h ) 1 60 gateway-ch-slv gateway-out-be@ ;
: prog-remain-h@ ( -- h ) 1 12 gateway-ch-slv gateway-in-be@ ;
\ set_m = actual_m (min)
: proc-time-m! ( m -- ) 1 61 gateway-ch-slv gateway-out-be! ;
: proc-time-m@ ( -- m ) 1 61 gateway-ch-slv gateway-out-be@ ;
: prog-remain-m@ ( -- m ) 1 13 gateway-ch-slv gateway-in-be@ ;
\ set_s = actual_s (sec)
: proc-time-s! ( s -- ) 1 62 gateway-ch-slv gateway-out-be! ;
: proc-time-s@ ( -- s ) 1 62 gateway-ch-slv gateway-out-be@ ;
: prog-remain-s@ ( -- s ) 1 14 gateway-ch-slv gateway-in-be@ ;


: program-no! ( n -- ) 1 4 gateway-ch-slv gateway-out-be! ;
: act-program-no@ ( -- n ) 1 4 gateway-ch-slv gateway-in-be@ ;


\ life word
: i-lifeword! ( w -- )
    2 70 gateway-ch-slv gateway-out-be!
;
: i-lifeword@ ( -- w )
    2 70 gateway-ch-slv gateway-out-be@
;
: q-lifeword@ ( -- w )
    2 70 gateway-ch-slv gateway-in-be@
;
: munk-lifeword-forth ( -- )
    i-lifeword@ 1 + i-lifeword!
;

: system-ready? ( -- t )
    ec-ready?
    gateway-ch-slv gateway-ready? and
;

: .system-status ( -- )
    ." system_ready|" system-ready? 0 .r cr
;

variable munk-status-step
: .munk-status ( -- )
    munk-status-step @
    case
        0 of
            ." munk_status|" munk-status@ 0 .r cr
            ." munk_manual_op_mode|" manual-op-mode@ 0 .r cr
            ." munk_set_v|"
            set-v1@ 0 .r ." ."
            set-v2@ 0 .r ." ."
            set-v3@ 0 .r ." ."
            set-v4@ 0 .r cr
        endof

        1 of
            ." munk_set_a|"
            set-a1@ 0 .r ." ."
            set-a2@ 0 .r ." ."
            set-a3@ 0 .r ." ."
            set-a4@ 0 .r cr

            ." munk_set_t|"
            set-t1@ 0 .r ." ."
            set-t2@ 0 .r ." ."
            set-t3@ 0 .r ." ."
            set-t4@ 0 .r cr
        endof

        2 of
            ." munk_ramp|"
            ramp-sec@ 0 .r ." ."
            ramp-start-perc@ 0 .r ." ."
            ramp-final-perc@ 0 .r cr

            ." munk_proc_time|"
            proc-time-h@ 0 .r ." ."
            proc-time-m@ 0 .r ." ."
            proc-time-s@ 0 .r cr

            ." munk_prog_remain|"
            prog-remain-h@ 0 .r ." ."
            prog-remain-m@ 0 .r ." ."
            prog-remain-s@ 0 .r cr
            
            ." lifeword|" q-lifeword@ 0 .r cr
        endof
    endcase

    munk-status-step @ 1 + 3 mod munk-status-step !
;

: munk-init ( -- )
    auto/manual munk-map!
    0 munk-cmd!
    munk-cmd-busy off
    -ready-to-op
    +switch-on
    -ack-process-end
    -ack-faults
;



\ munk forth
: munk-forth ( -- )
    system-ready?
    if
        munk-cmd-forth
        munk-lifeword-forth
    then
;