-work

\ ========== configurations ==========
variable current-req-done       true current-req-done !
variable current-req
variable current-data-handler
variable res-error
variable watchdog-clock-loose
variable watchdog-clock-exact

\ periodic requests
\ 格式為 (6 bytes device ID) + (-) + (4 bytes data ID ) + (-)
create req-0004
char0 , char0 , char0 , char0 , char0 , char2 , char- ,
char0 , char0 , char0 , char4 , char- ,

create req-0011
char0 , char0 , char0 , char0 , char0 , char2 , char- ,
char0 , char0 , char1 , char1 , char- ,

create req-0012
char0 , char0 , char0 , char0 , char0 , char2 , char- ,
char0 , char0 , char1 , char2 , char- ,

create req-0013
char0 , char0 , char0 , char0 , char0 , char2 , char- ,
char0 , char0 , char1 , char3 , char- ,

create req-0014
char0 , char0 , char0 , char0 , char0 , char2 , char- ,
char0 , char0 , char1 , char4 , char- ,

create req-0021
char0 , char0 , char0 , char0 , char0 , char2 , char- ,
char0 , char0 , char2 , char1 , char- ,

: push-periodic-reqs ( -- )
    \ 至少要啟用一個否則程式無法運行，目前只用到 0004
    req-0004 data-0004 push-req
    \ req-0011 data-0011 push-req
    \ req-0012 data-0012 push-req
    \ req-0013 data-0013 push-req
    \ req-0014 data-0014 push-req
    \ req-0021 data-0021 push-req
;



\ ========== steps ==========
variable communication-init-done
variable searching-input-$-done
variable input>res-header-done
variable input>data-buff-done

\ 初始設定
: communication-init
    communication-init-done @ not
    uart-ch-slv uart-ready? and
    if
        6 uart-ch-slv uart-baud!
        3 uart-ch-slv uart-frame!
        true communication-init-done !
    then

    watchdog-clock-loose renew
    watchdog-clock-exact renew
;
step communication-init

\ 初始設定後或重置後或完成一次通訊後等待 communication ready
: wait-communication-ready
    false res-error !
    false searching-input-$-done !
    false input>res-header-done !
    false input>data-buff-done !
;
step wait-communication-ready

\ 處理需不斷發送的 requests 和 req forth，並送出一個 request
: req>output
    \ 處理 periodic requests
    reqs-len @ 1 <
    if
        push-periodic-reqs
    then

    \ req forth
    current-req-done @
    if
        pop-req current-data-handler ! current-req !
        false current-req-done !
    then

    \ 送出 req，封包格式為 ($) + (12 bytes req) + (2 bytes req checksum) + (CR)
    char$ 1 uart-ch-slv uart-data!

    0 current-req @ 12 0                            ( 0 addr len 0 )
    ?do                                             ( sum addr )
        i cells over + @                            ( sum addr element )
        dup 1 uart-ch-slv uart-data!                ( sum addr element )
        rot + swap                                  ( sum+element addr )
    loop                                            ( sum addr )
    drop                                            ( sum )
    
    255 and dup 16 mod                              ( checksum checksum-low )
    swap over - 16 /                                ( checksum-low checksum-high )
    char0 + swap char0 + 2 uart-ch-slv uart-data!   ( )

    charCR 1 uart-ch-slv uart-data!
;
step req>output

\ 等待並判斷是否收到一個 ($)
: searching-input-$
    begin
        uart-ch-slv uart-rx-len@ 0>
        searching-input-$-done @ not and
    while
        1 uart-ch-slv uart-data@ char$ =
        if
            true searching-input-$-done !
        then
    repeat
;
step searching-input-$

\ 等待並判斷是否收到正確的 respond header
: input>res-header
    uart-ch-slv uart-rx-len@ 14 >=
    if
        true

        \ expect 6 bytes device ID
        6 0
        ?do
            1 uart-ch-slv uart-data@ current-req @ i param@ = and
        loop

        \ expect a (-)
        1 uart-ch-slv uart-data@ drop

        \ expext 4 bytes data ID
        1 uart-ch-slv uart-data@ 4 - current-req @ 7 param@ = and
        1 uart-ch-slv uart-data@ current-req @ 8 param@ = and
        1 uart-ch-slv uart-data@ 8 - current-req @ 9 param@ = and
        1 uart-ch-slv uart-data@ current-req @ 10 param@ = and

        \ expect 3 bytes others
        3 uart-ch-slv uart-data@ drop drop drop
        
        not
        if
            true res-error !
        then

        true input>res-header-done !
    then
;
step input>res-header

\ 等待並接收 res，格式為 (-) + (64 bytes data) + (-) + (2 bytes checksum) 共 68 bytes
: input>data-buff
    res-error @
    if
        true input>data-buff-done !
    else
        uart-ch-slv uart-rx-len@ 68 >=
        if
            0                                               ( 0 )

            \ expect a (-)
            1 uart-ch-slv uart-data@ +                      ( sum )

            64 0                                            ( sum 64 0 )
            ?do                                             ( sum )
                1 uart-ch-slv uart-data@                    ( sum element )
                swap over + swap                            ( sum+element element )
                data-buff @ i param!                        ( sum )
            loop                                            ( sum )

            \ expect a (-)
            1 uart-ch-slv uart-data@ +                      ( sum )

            255 and dup 16 mod                              ( checksum checksum-low )
            swap over - 16 /                                ( checksum-low checksum-high )

            1 uart-ch-slv uart-data@ char0 - = swap         ( flag checksum-low )
            1 uart-ch-slv uart-data@ char0 - = and not      ( flag )
            if
                true res-error !
            then

            true input>data-buff-done !
        then
    then
;
step input>data-buff

\ 處理收到的 data，若有 res error 就 reset uart，否則若 data 正確就和對應的 handler 交換 data
: data-buff>data
    res-error @
    if
        uart-ch-slv 0uart
    else
        current-data-handler @ @                    \ 取出 current-data-handler 中存放的 handler 持有的 data
        data-buff @ current-data-handler @ !        \ 將新的 data 放到 current-data-handler 中存放的 handler 中
        data-buff !                                 \ 將舊的 data 放到 data-buff 這個 handler 中
        true current-req-done !

        watchdog-clock-loose renew
        watchdog-clock-exact renew
        false communication-fault !
    then
;
step data-buff>data

\ 監聽 watchdog clock 並做通訊異常時的處理
: watchdog
    mtime watchdog-clock-exact @ - abs 500 >
    if
        uart-ch-slv 0uart
        ['] wait-communication-ready -step
        ['] req>output -step
        ['] searching-input-$ -step
        ['] input>res-header -step
        ['] input>data-buff -step
        ['] data-buff>data -step

        ['] wait-communication-ready +step
        false res-error !
        false searching-input-$-done !
        false input>res-header-done !
        false input>data-buff-done !
        
        watchdog-clock-exact renew

        mtime watchdog-clock-loose @ - abs 5000 >
        if
            ." error|LDPH communication not ready." cr
            ." log|LDPH communication trying to recovery." cr
            true communication-fault !

            watchdog-clock-loose renew
        then
    then
;
step watchdog



\ ========== transitions ==========
\ 判斷初始化是否已完成
: communication-init-done? ( -- flag )
    communication-init-done @
    uart-ch-slv uart-ready? and
;
transition communication-init-done?

\ 判斷是否 communication ready
: communication-ready? ( -- flag )
    uart-ch-slv uart-ready?
    ['] wait-communication-ready elapsed 50 > and
;
transition communication-ready?

\ 完成發送 request
: req>output-end? ( -- flag )
    true
;
transition req>output-end?

\ 判斷是否已完成接收 ($)
: searching-input-$-done? ( -- flag )
    searching-input-$-done @
;
transition searching-input-$-done?

\ 判斷是否已完成接收 reapond header
: input>res-header-done? ( -- flag )
    input>res-header-done @
;
transition input>res-header-done?

\ 判斷是否已完成接收 respond data
: input>data-buff-done? ( -- flag )
    input>data-buff-done @
;
transition input>data-buff-done?

\ 完成處理接收到的 data
: data-buff>data-end? ( -- flag )
    true
;
transition data-buff>data-end?



\ =============== SFC ===============
\
\          communication-init
\                  |
\                  + communication-init-done?
\                  |
\                  +------------------------------------+
\                  |                                    |
\                  v                                    v
\       wait-communication-ready                     watchdog
\                  |
\                  + communication-ready?
\                  |
\                  v
\              req>output
\                  |
\                  + req>output-end?
\                  |
\                  v
\          searching-input-$
\                  |
\                  + searching-input-$-done?
\                  |
\                  v
\           input>res-header
\                  |
\                  + input>res-header-done?
\                  |
\                  v
\           input>data-buff
\                  |
\                  + input>data-buff-done?
\                  |
\                  v
\            data-buff>data
\                  |
\                  + data-buff>data-end?
\                  |
\                  v
\       wait-communication-ready
\

' communication-init        ' communication-init-done? -->      ' communication-init-done?  ' wait-communication-ready -->
                                                                ' communication-init-done?  ' watchdog -->
' wait-communication-ready  ' communication-ready? -->          ' communication-ready?      ' req>output -->
' req>output                ' req>output-end? -->               ' req>output-end?           ' searching-input-$ -->
' searching-input-$         ' searching-input-$-done? -->       ' searching-input-$-done?   ' input>res-header -->
' input>res-header          ' input>res-header-done? -->        ' input>res-header-done?    ' input>data-buff -->
' input>data-buff           ' input>data-buff-done? -->         ' input>data-buff-done?     ' data-buff>data -->
' data-buff>data            ' data-buff>data-end? -->           ' data-buff>data-end?       ' wait-communication-ready -->



\ ========== test commands ==========
: .dat ( addr -- )
    64 0
    ?do
        dup i cells + @ .
    loop
    drop
;

: emit-dat ( addr -- )
    64 0
    ?do
        dup i cells + @ emit
    loop
    drop
;

: 0dat ( addr -- )
    64 0
    ?do
        0 over i cells + !
    loop
    drop
;

: .data
    ." 0004|" data-0004 @ .dat cr
    ." 0011|" data-0011 @ .dat cr
    ." 0012|" data-0012 @ .dat cr
    ." 0013|" data-0013 @ .dat cr
    ." 0014|" data-0014 @ .dat cr
    ." 0021|" data-0021 @ .dat cr
    ." buff|" data-buff @ .dat cr
;

: emit-data
    ." 0004|" data-0004 @ emit-dat cr
    ." 0011|" data-0011 @ emit-dat cr
    ." 0012|" data-0012 @ emit-dat cr
    ." 0013|" data-0013 @ emit-dat cr
    ." 0014|" data-0014 @ emit-dat cr
    ." 0021|" data-0021 @ emit-dat cr
    ." buff|" data-buff @ emit-dat cr
;

: 0data
    data-0004 @ 0dat
    data-0011 @ 0dat
    data-0012 @ 0dat
    data-0013 @ 0dat
    data-0014 @ 0dat
    data-0021 @ 0dat
    data-buff @ 0dat
;

marker -work