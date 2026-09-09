-work

\ ========== EtherCAT configurations ==========
\ EL1808 din
1 2 2constant EMS-din
2 2 2constant system-off-din
3 2 2constant 3phase-din
4 2 2constant sol3-level-din

\ EL2808 dout
1 3 2constant system-off-dout
2 3 2constant buzzer-dout
3 3 2constant lamp-green-dout
4 3 2constant lamp-yellow-dout
5 3 2constant lamp-red-dout
6 3 2constant aeration-pressure-op-dout
7 3 2constant m4-op-dout
8 3 2constant LDPH-op-dout

\ EL3052 current ain
1 4 2constant cr6-level-ain
2 4 2constant cr6-pH-ain

\ EL4002 voltage aout
1 5 2constant m4-rpm-aout
2 5 2constant m3-spm-aout

\ EL6021
1 6 2constant uart-ch-slv

\ EL2808 dout
1 7 2constant cr6-level-high-dout        \ 六價鉻桶高液位檢知
2 7 2constant cr6-level-low-dout         \ 六價鉻桶低液位檢知
3 7 2constant cr6-onReady-dout           \ 六價鉻系統準備通知


\ ===== global flags =====
variable cr6-state                                  \ not_ready=0, idle=1, processing=2, finished=3
variable pH-pre-state                               \ not_ready=0, idle=1，processing=2，finished=3
variable pH-post-state                              \ not_ready=0, idle=1，processing=2，finished=3
variable system-ready
variable system-EMS
variable system-3phase-error
variable sol3-level-error
variable cr6-level-error
variable communication-fault



\ ========== global commands ==========
: param@ ( addr idx -- val )
    cells + @
;

: param! ( val addr idx -- )
    cells + !
;

: fparam@ ( addr idx -- )( F: -- val )
    floats swap faligned + f@
;

: fparam! ( addr idx -- )( F: val -- )
    floats swap faligned + f!
;

: on ( ch slv -- )
    1 -rot ec-dout!
;

: off ( ch slv -- )
    0 -rot ec-dout!
;

: renew ( clock-addr -- )
    mtime swap !
;

: accepted? ( req-addr -- flag )
    dup @ false rot !
;



\ ========== communication parameters ==========
\ ASCII code
: charCR 13 ;
: char$ 36 ;
: char- 45 ;
: char0 48 ;
: char1 49 ;
: char2 50 ;
: char3 51 ;
: char4 52 ;
: char5 53 ;
: char6 54 ;
: char7 55 ;
: char8 56 ;
: char9 57 ;
: char: 58 ;
: char; 59 ;
: char< 60 ;
: char= 61 ;
: char> 62 ;
: char? 63 ;

\ request queue
variable reqs-cap
variable reqs-len
variable reqs-ptr
create reqs 64 dup 2 * cells allot reqs-cap !

: reqs-space@ ( -- space )
    reqs-cap @ reqs-len @ -
;

: reqs-element@ ( idx -- req res-handler )
    reqs-ptr @ + reqs-cap @ mod 2 * cells
    reqs +
    dup @ swap 1 cells + @
;

: reqs-element! ( req data-handler idx -- )
    reqs-ptr @ + reqs-cap @ mod 2 * cells
    reqs +
    swap over 1 cells + !
    !
;

: pop-req ( -- req data-handler )
    0 reqs-element@
    reqs-len @ 1- reqs-len !
    reqs-ptr @ 1+ reqs-cap @ mod reqs-ptr !
;

: push-req ( req data-handler -- )
    reqs-len @ reqs-element!
    reqs-len @ 1+ reqs-len !
;

: .reqs ( -- )
    reqs-len @ 0
    ?do
        i reqs-element@
        swap . .
    loop
;

: 0reqs ( -- )
    0 reqs-len !
    0 reqs-ptr !
;

\ data handlers
variable data-0004
variable data-0011
variable data-0012
variable data-0013
variable data-0014
variable data-0021
variable data-buff

here data-0004 ! 64 cells allot
here data-0011 ! 64 cells allot
here data-0012 ! 64 cells allot
here data-0013 ! 64 cells allot
here data-0014 ! 64 cells allot
here data-0021 ! 64 cells allot
here data-buff ! 64 cells allot



\ ========== Cr6 system parameters ==========
variable pH-balance-cycle 600000 pH-balance-cycle !         \ 酸鹼平衡靜置時間

\ pH
fvariable cr6-pH
fvariable pre-pH-UB 11.0e pre-pH-UB f!                       \ 前處理 pH 上臨界值
fvariable pre-pH-LB 10.0e pre-pH-LB f!                         \ 前處理 pH 下臨界值
fvariable post-pH-UB 8e post-pH-UB f!                       \ 後處理 pH 上臨界值
fvariable post-pH-LB 6.5e post-pH-LB f!                     \ 後處理 pH 下臨界值

\ 廢液槽水位
fvariable cr6-level
fvariable cr6-level-UB 0.8e  cr6-level-UB f!                \ 高水位限制
fvariable cr6-level-LB 0.3e  cr6-level-LB f!                \ 低水位限制

: m4-rpm! ( F: rpm -- )
    0e fmax 15e fmin
    15e f/
    2e 15e f** 1e f- 10e f/ f*
    f>s m4-rpm-aout ec-aout!
;

: mixer ( S: boolean -- )
    if m4-op-dout on
    else m4-op-dout off
    then
;

: aeration ( S: boolean -- )
    if  aeration-pressure-op-dout on
    else aeration-pressure-op-dout off
    then
;

: cr6-T@ ( F: -- T )
    data-0004 @                         ( addr )
    dup 43 param@ char0 -               ( addr sum )
    over 42 param@ char0 - 16 * +       ( addr sum )
    over 47 param@ char0 - 256 * +      ( addr sum )
    swap 46 param@ char0 - 512 * +      ( sum )
    s>f 10e f/                          ( ) ( F: T )
;

: cr6-L@ ( F: -- L )
    \ cr6 槽直徑=1.965m
    1.965e fdup f* pi f* cr6-level f@ f* 4e f/ 1000e f*
;

: cr6-H@ ( F: -- H )
    cr6-level f@
;

: .cr6-status ( -- )
    ." Cr6_pH|" cr6-pH f@ f. ." |"
    ." Cr6_T|" cr6-T@ f. ." |"
    ." Cr6_L|" cr6-L@ f. ." |"
    ." Cr6_H|" cr6-H@ f. ." |"
    ." Cr6_state|" cr6-state @
    case
        1 of ." Idle" endof
        2 of ." processing" endof
        ." finished"
    endcase
    cr
;

: +lamp-green ( -- )
    lamp-green-dout on
    lamp-yellow-dout off
    lamp-red-dout off
;

: +lamp-yellow ( -- )
    lamp-green-dout off
    lamp-yellow-dout on
    lamp-red-dout off
;

: +lamp-red ( -- )
    lamp-green-dout off
    lamp-yellow-dout off
    lamp-red-dout on
;



marker -work