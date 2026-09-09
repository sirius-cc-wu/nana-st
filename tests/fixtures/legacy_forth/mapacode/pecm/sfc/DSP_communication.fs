\ ===== 備註 =====
\ 此處還有些測試用命令，上機後需移除。

-work

\ 寫入 1 byte 長度的資料到封包中對應的位置
: byte! ( value DUAddr byteAddr -- )
    + c!
;

\ 以 big-endian 方式寫入 2 byte 長度的資料
: 2byte! ( value DUAddr byteAddr -- )
    2 pick 255 8 lshift and 8 rshift    ( value DUAddr byteAddr valHi )
    2 pick 2 pick byte!                 ( value DUAddr byteAddr )
    rot 255 and                         ( DUAddr byteAddr valLo )
    -rot 1 + byte!                      ( )
;

\ 取出封包中對應位置的 1 byte 長度的資料
: byte@ ( DUAddr byteAddr -- value )
    + c@
;

\ 以 big-endian 方式取出 2 byte 長度的資料
: 2byte@ ( DUAddr byteAddr -- value )
    over over byte@                     ( DUAddr byteAddr valHi )
    -rot 1 + byte@                      ( valHi valLo )
    swap 8 lshift or                    ( value )
;

\ 計算封包中資料的 checksum 值
: checksum ( DUAddr DUsize -- checksum )
    0 0                                 ( DUAddr DUsize 0 0 )
    begin                               ( DUAddr DUsize sum n )
        2 pick over >                   ( DUAddr DUsize sum n flag )
    while                               ( DUAddr DUsize sum n )
        3 pick over byte@ rot +         ( DUAddr DUsize n sum_uncheck )
        dup 255 >                       ( DUAddr DUsize n sum_uncheck flag )
        if                              ( DUAddr DUsize n sum_uncheck )
            255 -                       ( DUAddr DUsize n sum' )
        then                            ( DUAddr DUsize n sum' )
        swap 1 +                        ( DUAddr DUsize sum n+1 )
    repeat                              ( DUAddr DUsize sum n )
    drop -rot drop drop                 ( sum )
    invert 255 and                      ( checksum )
;



\ ===== configuration =====
2 constant DSP-header-size
13 constant DSP-TDU-size    \ Checksum byte included.
13 constant DSP-RDU-size    \ Checksum byte included.

\ ----- header -----
create DSP-header
DSP-header-size allot

\ ----- TDU -----
create DSP-TDU
DSP-TDU-size allot

\ ----- RDU -----
create DSP-RDU
DSP-RDU-size allot

create DSP-RDU-buf
DSP-RDU-size allot

255 DSP-header 0 byte!
255 DSP-header 1 byte!



\ ===== Header commands =====

\ 發送 header 給 DSP
: header>DSP ( -- )
    DSP-header-size 0               ( size 0 )
    begin                           ( size n )
        over over >                 ( size n flag )
    while                           ( size n )
        DSP-header over byte@       ( size n val )
        1 PECM-DSP-ch-slv uart-data!
        1 +                         ( size n+1 )
    repeat                          ( size n )
    drop drop                       ( )
;

\ 印出 header 的內容
: .DSP-header ( -- )
    DSP-header-size 0           ( size 0 )
    begin                       ( size n )
        over over >             ( size n flag )
    while                       ( size n )
        DSP-header over byte@ .
        1 +                     ( size n+1 )
    repeat                      ( size n )
    drop drop                   ( )
;

\ 檢查 input queue 的開頭是否為 header
\ 執行時先檢查 UART-rx-len 是否 >= 2，若是就逐一拿出 1 byte 資料與 header 比較，若全部比較正確
\ 就在堆疊上留下 true，若其中某次比較失敗就停止比較並在堆疊上留下 false
: DSP-check-header ( -- flag )
    PECM-DSP-ch-slv uart-rx-len@ DSP-header-size < invert
    if
        false DSP-header-size 0     ( false size 0 )
        begin                       ( flag size n )
            over over >
            if
                DSP-header over byte@ 1 PECM-DSP-ch-slv uart-data@ =
            else
                false
            then
        while                       ( flag size n )
            over 1 - over =
            if                      ( flag size n )
                rot drop true -rot  ( true size n )
            then
            1 +                     ( flag size n+1 )
        repeat                      ( flag size n )
        drop drop                   ( flag )
    else
        false                       ( flag )
    then
;



\ ===== DSP TDU commands =====

: Discharge-on! ( on=1|off=0 -- )
    DSP-TDU 0 byte!
;

: Real-current! ( 2bytes -- )
    DSP-TDU 1 2byte!
;

: MDU-on! ( on=1|off=0 -- )
    DSP-TDU 3 byte!
;

: Power-mode! ( 0|1|2|3 -- )
    DSP-TDU 4 byte!
;

: ON! ( 2bytes -- )
    DSP-TDU 5 2byte!
;

: OFF! ( 2bytes -- )
    DSP-TDU 7 2byte!
;

: Short-recovery-time! ( 1byte -- )
    DSP-TDU 9 byte!
;

: DAC-offset! ( 1byte -- )
    DSP-TDU 10 byte!
;

: Short-detect-level! ( 1byte -- )
    DSP-TDU 11 byte!
;

\ TODO: 暫時給定的預設值
2 Power-mode!
200 ON!
800 OFF!
30 real-current!
100 Short-recovery-time!        
10 DAC-offset!                
15 Short-detect-level!

\ 計算 TDU 的 checksum 值並存入 TDU 的最後一個 byte 中
: checksum>TDU ( -- )
    0 DSP-TDU DSP-TDU-size 1 - byte!
    DSP-TDU DSP-TDU-size checksum
    DSP-TDU DSP-TDU-size 1 - byte!
;

\ 發送 TDU 給 DSP
: TDU>DSP ( -- )
    DSP-TDU-size 0                  ( size 0 )
    begin                           ( size n )
        over over >                 ( size n flag )
    while                           ( size n )
        DSP-TDU over byte@          ( size n val )
        1 PECM-DSP-ch-slv uart-data! ( size n )
        1 +                         ( size n+1 )
    repeat                          ( size n )
    drop drop                       ( )
;

\ 印出 TDU 的內容
: .DSP-TDU ( -- )
    DSP-TDU-size 0              ( size 0 )
    begin                       ( size n )
        over over >             ( size n flag )
    while                       ( size n )
        DSP-TDU over byte@ .
        1 +                     ( size n+1 )
    repeat                      ( size n )
    drop drop                   ( )
;


\ ===== DSP RDU commands =====

: Discharge-on-ready@ ( -- ready=1 )
    DSP-RDU 0 byte@
;

: PEM-Ready-to-work@ ( -- ready=1 )
    DSP-RDU 1 byte@
;

: Ready-switch-current-enable@ ( -- 1byte )
    DSP-RDU 2 byte@
;

: Unit-status@ ( -- 1byte )
    DSP-RDU 3 byte@
;

: PORT+OVP+TMP-WAR@ ( -- 1byte )
    DSP-RDU 4 byte@
;

: ID-code@ ( -- 1byte )
    DSP-RDU 5 byte@
;

: V-avg@ ( -- 2bytes )
    DSP-RDU 6 2byte@
;

: I-avg@ ( -- 2bytes )
    DSP-RDU 8 2byte@
;

: Effect-t@ ( -- 1byte )
    DSP-RDU 10 byte@
;

: Short-count@ ( -- 1byte )
    DSP-RDU 11 byte@
;

: DSP-RDU-buf-checksum@ ( -- checksum )
    DSP-RDU-buf DSP-RDU-size checksum
;

\ 接收來自 DSP 的封包並寫入 RDU-buf 中
: DSP>RDU-buf
    DSP-RDU-size 0                  ( size 0 )
    begin                           ( size n )
        over over >                 ( size n flag )
    while                           ( size n )
        1 PECM-DSP-ch-slv uart-data@ ( size n val )
        DSP-RDU-buf 2 pick byte!    ( size n )
        1 +                         ( size n+1 )
    repeat                          ( size n )
    drop drop                       ( )
;

\ 將 RDU-buf 的資料 copy 到 RDU 中
: RDU-buf>RDU
    DSP-RDU-size 0              ( size 0 )
    begin                       ( size n )
        over over >             ( size n flag )
    while                       ( size n )
        DSP-RDU-buf over byte@  ( size n value )
        DSP-RDU 2 pick byte!    ( size n )
        1 +                     ( size n+1 )
    repeat                      ( size n )
    drop drop                   ( )
;

\ 印出 RDU 的內容
: .DSP-RDU ( -- )
    DSP-RDU-size 0              ( size 0 )
    begin                       ( size n )
        over over >             ( size n flag )
    while                       ( size n )
        DSP-RDU over byte@ .
        1 +                     ( size n+1 )
    repeat                      ( size n )
    drop drop                   ( )
;

\ 印出 RDU-buf 的內容
: .DSP-RDU-buf ( -- )
    DSP-RDU-size 0              ( size 0 )
    begin                       ( size n )
        over over >             ( size n flag )
    while                       ( size n )
        DSP-RDU-buf over byte@ .
        1 +                     ( size n+1 )
    repeat                      ( size n )
    drop drop                   ( )
;

\ statuses
$1 constant PROT
$2 constant OVP
$4 constant TmpWarn
$8 constant NegVol
$10 constant CircuitFail
$80 constant PEMError

variable pem-test
: is-PROT? ( -- flag ) PROT PORT+OVP+TMP-WAR@ and 0<> ;
: is-OVP? ( -- flag ) OVP PORT+OVP+TMP-WAR@ and 0<> ;
: is-TmpWarn? ( -- flag ) TmpWarn PORT+OVP+TMP-WAR@ and 0<> ;
: is-NegVol? ( -- flag ) NegVol PORT+OVP+TMP-WAR@ and 0<> ;
: is-CircuitFail? ( -- flag ) CircuitFail PORT+OVP+TMP-WAR@ and 0<> ;
: is-PEMError? ( -- flag ) PEMError PORT+OVP+TMP-WAR@ and 0<> ;

\ ===== SFC DSP_communication =====
\ 說明：
\   此 SFC 處理與 DSP 通訊之相關工作
\
\ 流程：
\   1. 在 S1 等待至 UART ready 後，進行 UART 初始設定
\   2-1. S2 等待一段延遲後，計算 TDU 之 checksum 值，並發送 header 和 TDU，然後回到 S2 等待下一個循環開始 
\   2-2-1. S4 等待資料進來並判斷是否為 header，若是就
\   2-2-2. S5 等待到收到的資料達完整封包長度，然後對收到的資料進行 checksum 校驗
\   2-2-3. 若校驗成功就將該資料存入 RDU，然後回到 S4 進行下一個循環
\
\ 異常處理：
\   1. S-watchdog 不斷疊加 counter，若未正常與 DSP 通訊並清除 counter 直到 counter 累計達 200，就開啟 
\   DSP-fault 旗標
\   2. 若未正常與 DSP 通訊並清除 counter 直到 counter 累計達 2000，就 reset UART 並印出異警訊息
\
\ SFC：
\
\      T1       T2          T3
\ S1---+--->S2--+--+-->S3---+--->S4
\                  |   ^          |
\                  |   |    T4    |
\                  |   +----+-----+
\                  |
\                  |        T5        T6
\                  +-->S5---+--->S6---+--->S7
\                  |   ^                    |
\                  |   |         T7         |
\                  |   +---------+----------+
\                  |
\                  |            T-watchdog
\                  +-->S-watchdog---+--->S2
\
\ -----------+----------------+---------------------------------------------------------------------
\ S1         | DSP-S1         | UART 初始設定
\ -----------+----------------+---------------------------------------------------------------------
\ S2         | DSP-S2         | waiting
\ -----------+----------------+---------------------------------------------------------------------
\ S3         | DSP-S3         | waiting
\ -----------+----------------+---------------------------------------------------------------------
\ S4         | DSP-S4         | 計算 TDU 之 checksum 值，然後發送 header 和 TDU 給 DSP，然後判斷是否
\            |                | DSP-hold = true，是就設定 DSP-hold = false，DSP-suspend = true 進入暫停
\ -----------+----------------+---------------------------------------------------------------------
\ S5         | DSP-S5         | 等待並判斷收到的 header
\ -----------+----------------+---------------------------------------------------------------------
\ S6         | DSP-S6         | 等待並接收封包
\ -----------+----------------+---------------------------------------------------------------------
\ S7         | DSP-S7         | 對 RDU-buf 進行 checksum 校驗，若校驗成功就將 RDU-buf 的資料放入 RDU 並更新
\            |                | DSP-watchdog 和清除 DSP-fault 旗標
\ -----------+----------------+---------------------------------------------------------------------
\ S-watchdog | DSP-S-watchdog | 檢查當下時間和 DSP-watchdog 相差是否大於 200 ms，是就開啟DSP-fault 旗標，
\            |                | 並印出異警
\ -----------+----------------+---------------------------------------------------------------------
\
\ -----------+----------------+---------------------------------------------------------------------
\ T1         | DSP-T1         | 等待 DSP-S1-done = true 後 reset UART 並 transpose
\ -----------+----------------+---------------------------------------------------------------------
\ T2         | DSP-T2         | 判斷是否 DSP-suspend = true 且延遲已完成 或 DSP-suspend = false，
\            |                | 且 UART ready，是就更新 DSP-watchdog，關閉 DSP-suspend 旗標並 tranpose
\ -----------+----------------+---------------------------------------------------------------------
\ T3         | DSP-T3         | 判斷延遲 800 ms
\ -----------+----------------+---------------------------------------------------------------------
\ T4         | DSP-T4         | true
\ -----------+----------------+---------------------------------------------------------------------
\ T5         | DSP-T5         | 判斷 DSP-S5 完成後 transpose
\ -----------+----------------+---------------------------------------------------------------------
\ T6         | DSP-T6         | 等待 DSP-S6 完成後 transpose
\ -----------+----------------+---------------------------------------------------------------------
\ T7         | DSP-T7         | true
\ -----------+----------------+---------------------------------------------------------------------
\ T-watchdog | DSP-T-watchdog | 檢查當下時間和 DSP-watchdog 的差異若 > 2000 ms 就設定 reset UART 並印出訊
\            |                | 息，然後判斷 DSP-suspend = true，其一成立就 deactivate DSP-S3,4,5,6,7，
\            |                | 然後 transpose
\ -----------+----------------+---------------------------------------------------------------------

variable DSP-S1-done
variable DSP-S5-done
variable DSP-S6-done
variable DSP-watchdog
variable DSP-fault
variable DSP-hold
variable DSP-suspend
variable DSP-suspend-interval  5000 DSP-suspend-interval !

: +DSP
    DSP-hold off
    DSP-suspend off
;

: -DSP
    DSP-hold on
;

: DSP-S1
    PECM-DSP-ch-slv uart-ready?
    DSP-S1-done @ not and
    if
        6 PECM-DSP-ch-slv uart-baud!
        3 PECM-DSP-ch-slv uart-frame!
        DSP-S1-done on
    then
;

: DSP-S2 ( waiting ) ;

: DSP-S3 ( waiting ) ;

: DSP-S4
    checksum>TDU header>DSP TDU>DSP
    DSP-hold @
    if
        DSP-suspend on
        DSP-hold off
    then
;

: DSP-S5
    DSP-check-header
    if
        DSP-S5-done on
    then
;

: DSP-S6
    PECM-DSP-ch-slv uart-rx-len@ DSP-RDU-size < invert
    if
        DSP>RDU-buf
        DSP-S6-done on
    then
;

: DSP-S7
    DSP-RDU-buf-checksum@ 0 =
    if
        mtime DSP-watchdog !
        DSP-fault off
        RDU-buf>RDU
    then
;

: DSP-S-watchdog
    DSP-fault @ not
    if
        \ TODO: 門檻暫時改為 2000 ms
        mtime DSP-watchdog @ - 2000 >
        if
            DSP-fault on
            ." error|DSP communication not ready.;A3101" cr
        then
    then
;

: DSP-T1
    DSP-S1-done @
    dup if
        PECM-DSP-ch-slv 0uart
    then
;

: DSP-T2
    DSP-suspend @
    if
        ['] DSP-S2 elapsed DSP-suspend-interval @ >
    else
        true
    then

    PECM-DSP-ch-slv uart-ready? and
    dup if
        mtime DSP-watchdog !
        DSP-suspend off
    then
;

\ TODO: 通訊週期暫時改為 800 ms
: DSP-T3 ['] DSP-S3 elapsed 800 > ;

: DSP-T4 true ;

: DSP-T5
    DSP-S5-done @
    if
        DSP-S5-done off
        true
    else
        false
    then
;

: DSP-T6
    DSP-S6-done @
    if
        DSP-S6-done off
        true
    else
        false
    then
;

: DSP-T7 true ;

: DSP-T-watchdog
    \ TODO: 嘗試週期暫時改為 5000 ms
    mtime DSP-watchdog @ - 5000 >
    dup if
    \    ." log|DSP communication trying to recovery." cr
        PECM-DSP-ch-slv 0uart
    then

    DSP-suspend @ or

    dup if
        ['] DSP-S3 -step
        ['] DSP-S4 -step
        ['] DSP-S5 -step
        ['] DSP-S6 -step
        ['] DSP-S7 -step
    then
;

step DSP-S1
step DSP-S2
step DSP-S3
step DSP-S4
step DSP-S5
step DSP-S6
step DSP-S7
step DSP-S-watchdog

transition DSP-T1
transition DSP-T2
transition DSP-T3
transition DSP-T4
transition DSP-T5
transition DSP-T6
transition DSP-T7
transition DSP-T-watchdog

' DSP-S1 ' DSP-T1 -->
' DSP-T1 ' DSP-S2 -->
' DSP-S2 ' DSP-T2 -->
' DSP-T2 ' DSP-S3 -->
' DSP-T2 ' DSP-S5 -->
' DSP-T2 ' DSP-S-watchdog -->

' DSP-S3 ' DSP-T3 -->
' DSP-T3 ' DSP-S4 -->
' DSP-S4 ' DSP-T4 -->
' DSP-T4 ' DSP-S3 -->

' DSP-S5 ' DSP-T5 -->
' DSP-T5 ' DSP-S6 -->
' DSP-S6 ' DSP-T6 -->
' DSP-T6 ' DSP-S7 -->
' DSP-S7 ' DSP-T7 -->
' DSP-T7 ' DSP-S5 -->

' DSP-S-watchdog ' DSP-T-watchdog -->
' DSP-T-watchdog ' DSP-S2 -->



\ 印出電源通訊狀態
: .DSP-fault ( -- )
    DSP-fault @
    if
        ." DSP_fault|true" cr
    else
        ." DSP_fault|false" cr
    then
;

\ 印出PEM相關訊息
: .PEM-message       ( -- )
    ." PEM_message|" PORT+OVP+TMP-WAR@ . cr
;


marker -work