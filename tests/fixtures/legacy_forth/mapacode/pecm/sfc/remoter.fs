-work

\ ===== Configurations =====
\ ----- variables -----
variable RMT-busy
variable RMT-fault
variable RMT-watchdog
variable RMT-double-check-key
variable RMT-accepted-key
variable RMT-cur-ptr
variable RMT-feed-leds-D9
variable RMT-feed-leds-DA
variable RMT-feed-leds-DB
variable RMT-feed-leds-DC
variable RMT-feed-leds-DD
variable RMT-feed-leds-DE
variable RMT-OT-release
variable RMT-jog-continue
variable RMT-jog-end-key
variable RMT-jogging-axis

\ 開關DL與DR是否可jog
variable DLDR-op-state

\ ----- fvariables -----
fvariable RMT-jog-direction
fvariable RMT-jog-distance
fvariable RMT-jog-velocity

\ ----- End point step -----
: RMT-Send ( Do nothing ) ;
step RMT-Send



\ ===== Commands =====
\ 求商和餘數
: divide ( dividend divisor -- remainder quotient )
    0 -rot swap             ( 0 divisor dividend )
    begin                   ( n divisor dividend )
        over over > not     ( n divisor dividend flag )
    while                   ( n divisor dividend )
        over -              ( n divisor dividend-divisor )
        rot 1+ -rot         ( n+1 divisor dividend )
    repeat                  ( n divisor dividend )
    -rot drop               ( remainder quotient )
;

\ 設定對應的 LED 狀態
: RMT-led! ( bool key -- )
    1 -                         ( bool led-32 )
    8 divide                    ( bool bP n )
    case
        0 of RMT-feed-leds-DA endof
        1 of RMT-feed-leds-DB endof
        2 of RMT-feed-leds-DC endof
        3 of RMT-feed-leds-DD endof
        4 of RMT-feed-leds-DE endof
        5 of RMT-feed-leds-D9 endof
    endcase                     ( bool bP addr )
    -rot 2 pick @               ( addr bool bP val )
    1 2 pick lshift invert and  ( addr bool bP val' )
    -rot lshift or swap !       ( )
;
\ 放入 RMT-led! 令牌以建立向量執行
' RMT-led! 'RMT-led! !

\ 取得 remoter jog velocity matrix 中的設定值
: RMT-velocity@ ( no -- ) ( F: -- velocity )
    1- 6 * RMT-cur-ptr @ 1- + floats RMT-velocity faligned + f@
;

\ 處理正反器
: RMT-ffs-forth ( -- )
    \ 忽略碰邊正反器
    touch-ignore @ dup                  \ 忽略碰邊
    ff-touch-ignore-re ff-forth-uc
    ff-touch-ignore-fe ff-forth-uc

    ff-touch-ignore-re ff-triggered-uc? \ 若忽略碰邊 rising edge 觸發
    if
        1 12 rmt-led!                       \ 開啟遙控器 ignore LED
    then

    ff-touch-ignore-fe ff-triggered-uc? \ 若忽略碰邊 falling edge 觸發
    if
        0 12 rmt-led!                       \ 關閉遙控器 ignore LED
    then

    \ 忽略碰邊逾時正反器
    PECM-motion-state @ idle =
    touch-ignore @ and                  \ 忽略碰邊且閒置
    ff-touch-ignore-expired-hl ff-forth-uc

    ff-touch-ignore-expired-hl ff-triggered-uc? \ 若忽略碰邊逾時 high level 觸發
    if
        touch-ignore off                    \ 自動關閉忽略碰邊
    then
;

\ ==================== uncheck ====================
\ 說明:
\   處理直接操作且無須等待的工作
\ 
\ SFC:
\                 RMT-S1
\                   |
\                   + RMT-uncheck-accepted?
\                   |
\                   v
\              RMT-uncheck
\                   |
\                   + RMT-uncheck-done?
\                   |
\                   v
\                 RMT-S1
\

\ 關閉 jog x1, x10, x100, L, H, R 模式按鍵的 LED
: 0jog-mode-leds ( -- )
    0 21 RMT-led!
    0 24 RMT-led!
    0 25 RMT-led!
    0 28 RMT-led!
    0 29 RMT-led!
    0 32 RMT-led!
;


\ -------------------- steps --------------------
\ 根據 accepted-key 決定處理什麼工作
: RMT-uncheck ( -- )
    RMT-accepted-key @
    case
        \ x1 按鍵
        29 of
            0jog-mode-leds
            1 29 RMT-led!
            1 RMT-cur-ptr !
            RMT-jog-continue off
            0.001e mm RMT-jog-distance f!
        endof

        \ x10 按鍵
        25 of
            0jog-mode-leds
            1 25 RMT-led!
            2 RMT-cur-ptr !
            RMT-jog-continue off
            0.01e mm RMT-jog-distance f!
        endof

        \ x100 按鍵
        21 of
            0jog-mode-leds
            1 21 RMT-led!
            3 RMT-cur-ptr !
            RMT-jog-continue off
            0.1e mm RMT-jog-distance f!
        endof
        
        \ L 按鍵
        32 of
            0jog-mode-leds
            1 32 RMT-led!
            4 RMT-cur-ptr !
            RMT-jog-continue on
            500e mm RMT-jog-distance f!
        endof
        
        \ H 按鍵
        28 of
            0jog-mode-leds
            1 28 RMT-led!
            5 RMT-cur-ptr !
            RMT-jog-continue on
            500e mm RMT-jog-distance f!
        endof
        
        \ R 按鍵
        24 of
            0jog-mode-leds
            1 24 RMT-led!
            6 RMT-cur-ptr !
            RMT-jog-continue on
            500e mm RMT-jog-distance f!
        endof

        \ clamp 按鍵
        04 of
            1 4 RMT-led!
            0 8 RMT-led!
            +chuck
        endof

        \ unclamp 按鍵
        08 of
            0 4 RMT-led!
            1 8 RMT-led!
            -chuck
        endof

        \ start 按鍵
        01 of
            remoter-start trigger-job!  \ 設定 trigger job 為 remoter start
        endof

        \ pause 按鍵
        02 of
            stop-motion
        endof

        \ reset 按鍵
        05 of
            abort-motion
        endof
        
        \ 返回入口 按鍵
        19 of
            trace-to-hold
        endof

        \ 拉起入口 按鍵
        20 of
            trace-to-start
        endof
        
        \ 只有在idle時 才可手動開關水
        15 of
            PECM-motion-state @ idle =
            machining-mode @ none = and if
                $aqua-run @ 
                if
                    -aqua-run
                else
                    +aqua-run
                then
            then
        endof

        \ touch ignore 按鍵
        12 of
            PECM-motion-state @ idle =       \ 若 motion idle
            if
                touch-ignore not!               \ 將忽略碰邊旗標反向
            then
        endof

        \ xyz ABW 按鍵 用於開關量測旗標
        17 of
            PECM-motion-state @ idle =       \ 若 motion idle
            if
                remoter-measure? @ if
                    0 17 RMT-led!
                else
                    1 17 RMT-led!
                then
                remoter-measure? not!       \ 將旗標反向,並將對應的燈點亮:量測狀態亮,一般則為暗
            then
        endof

        \ 開關dl dr 軸移動功能
        10 of
            DLDR-op-state @ if
                DLDR-op-state off
                0 10 RMT-led!
            else
                DLDR-op-state on
                1 10 RMT-led!
            then
        endof
        \ 關閉上方的門
        38 of
            PECM-motion-state @ idle =
            if
                platform-door@ if
                    dldr-off dldr-op !
                    dldr-go on
                else
                    ." error|CNC security door not closed. Stop NC.;A1503" cr
                then
            then
        endof
        \ 打開上方的門
        39 of
            PECM-motion-state @ idle =
            if
                dldr-on dldr-op !
                dldr-go on
            then
        endof
    endcase
;
step RMT-uncheck

\ -------------------- transitions --------------------
\ 是否接受操作
: RMT-uncheck-accepted? ( -- flag )
    RMT-accepted-key @
    dup 29 =                \ x1 按鍵
    over 25 = or            \ x10 按鍵
    over 21 = or            \ x100 按鍵
    over 32 = or            \ L 按鍵
    over 28 = or            \ H 按鍵
    over 24 = or            \ R 按鍵
    over 04 = or            \ clamp 按鍵
    over 08 = or            \ unclamp 按鍵
    over 01 = or            \ start 按鍵
    over 02 = or            \ pause 按鍵
    over 05 = or            \ reset 按鍵
    over 19 = or            \ 返回入口 按鍵
    over 20 = or            \ 拉起入口 按鍵
    over 15 = or            \ oil 按鍵
    over 12 = or            \ touch ignore 按鍵
    over 17 = or            \ 開啟/關閉量測模式
    over 10 = or            \ 開啟上方門 dl dr jog功能
    over 38 = or
    over 39 = or
    over 49 = or
    swap drop

    RMT-busy @ not and
;
transition RMT-uncheck-accepted?

\ 操作完成
: RMT-uncheck-done? ( -- flag )
    true
;
transition RMT-uncheck-done?

\ ===== macro_3 =====
\ 說明：
\   當 T5 transpose 時進入此 macro, 進行手動運動相關控制.
\
\ 流程：
\   1. 根據收到的 jog 鍵碼設定 RMT-jogging-axis, RMT-jog-direction 和 RMT-jog-velocity 為對應的值
\   2. 根據 RMT-jog-velocity 設定差值器最大速度限制，並根據 RMT-jogging-axis 和 RMT-jog-direction 下出 jog 命令，
\   然後在 S3-2 等待
\   3. 若 T3-2 判斷 jog 按鍵已放開，或是發生遙控器通訊異警，則處理軸移動結束工作，其中若為連動模式則需 stop-motion
\   
\
\ SFC：
\
\ T5         T3-0            T3-1        T3-2        T3-3
\ +--->S3-0---+--->S3-1---+---+--->S3-2---+--->S3-3---+--->Send
\                         |
\                         |   T6
\                         +---+--->S1
\
\ -----+----------+-------------------------------------------------------------------------------------
\ S3-0 | RMT-S3-0 | 根據 RMT-accepted-key 設定 RMT-jogging-axis, RMT-jog-direction 和 RMT-jog-velocity
\ -----+----------+-------------------------------------------------------------------------------------
\ S3-1 | RMT-S3-1 | 根據 RMT-jogging-axis, RMT-jog-direction 和 RMT-jog-velocity，設定差值器最大速度限制，
\      |          | 並下出軸移動命令
\ -----+----------+-------------------------------------------------------------------------------------
\ S3-2 | RMT-S3-2 | waiting
\ -----+----------+-------------------------------------------------------------------------------------
\ S3-3 | RMT-S3-3 | 處理軸移動結束工作，若是連動中，則 stop-motion，然後清除 RMT-jogging-axis, 
\      |          | RMT-jog-direction 和 RMT-jog-velocity
\ -----+----------+-------------------------------------------------------------------------------------
\ Send | RMT-Send | do nothing
\ -----+----------+-------------------------------------------------------------------------------------
\ S1   | RMT-S1   |
\ -----+----------+-------------------------------------------------------------------------------------
\
\ -----+----------+-------------------------------------------------------------------------------------
\ T3-0 | RMT-T3-0 | true
\ -----+----------+-------------------------------------------------------------------------------------
\ T3-1 | RMT-T3-1 | true
\ -----+----------+-------------------------------------------------------------------------------------
\ T3-2 | RMT-T3-2 | 判斷按下的手動運動按鍵是否放開，或是 RMT-fault = true，是就 transpose
\ -----+----------+-------------------------------------------------------------------------------------
\ T3-3 | RMT-T3-3 | 關閉 RMT-busy 旗標，然後 transpose
\ -----+----------+-------------------------------------------------------------------------------------
\ T6   | RMT-T6   | true
\ -----+----------+-------------------------------------------------------------------------------------

: RMT-S3-0
    RMT-accepted-key @
    case
        22 of  
                DLDR-op-state @ if
                    PECM-DL-axis RMT-jogging-axis !  -1e RMT-jog-direction f!  5 RMT-velocity@ RMT-jog-velocity f!
                else
                    PECM-XA-axis RMT-jogging-axis !  -1e RMT-jog-direction f!  1 RMT-velocity@ RMT-jog-velocity f!
                then  
            endof 
        23 of  
                DLDR-op-state @ if
                    PECM-DL-axis RMT-jogging-axis !  1e RMT-jog-direction f!  5 RMT-velocity@ RMT-jog-velocity f!
                else
                    PECM-XA-axis RMT-jogging-axis !  1e RMT-jog-direction f!  1 RMT-velocity@ RMT-jog-velocity f!
                then  
            endof 
        26 of  
                DLDR-op-state @ if
                    PECM-DR-axis RMT-jogging-axis !  -1e RMT-jog-direction f!  6 RMT-velocity@ RMT-jog-velocity f!
                else
                    PECM-XB-axis RMT-jogging-axis !  -1e RMT-jog-direction f!  2 RMT-velocity@ RMT-jog-velocity f!
                then  
            endof 
        27 of  
                DLDR-op-state @ if
                    PECM-DR-axis RMT-jogging-axis !  1e RMT-jog-direction f!  6 RMT-velocity@ RMT-jog-velocity f!
                else
                    PECM-XB-axis RMT-jogging-axis !  1e RMT-jog-direction f!  2 RMT-velocity@ RMT-jog-velocity f!
                then  
            endof
        30 of  PECM-XC-axis RMT-jogging-axis !  -1e RMT-jog-direction f!  3 RMT-velocity@ RMT-jog-velocity f!  endof
        31 of  PECM-XC-axis RMT-jogging-axis !   1e RMT-jog-direction f!  3 RMT-velocity@ RMT-jog-velocity f!  endof
        34 of  PECM-W-axis RMT-jogging-axis !  -1e RMT-jog-direction f!  4 RMT-velocity@ RMT-jog-velocity f!  endof 
        35 of  PECM-W-axis RMT-jogging-axis !   1e RMT-jog-direction f!  4 RMT-velocity@ RMT-jog-velocity f!  endof
    endcase
;

: RMT-S3-1
    RMT-jogging-axis @ RMT-jog-velocity f@ jog-v!
    RMT-jogging-axis @ RMT-jog-distance f@ RMT-jog-direction f@ f* jog
;

: RMT-S3-2 ( Waiting ) ;

: RMT-S3-3
    RMT-jog-continue @
    PECM-motion-state @ dup idle =
    swap jogging = or and
    \ RMT-jog-continue && (ECM-motion-state = idle || ECM-motion-state = jogging)
    if
        stop-motion
    then
    0 RMT-jogging-axis !
    0e RMT-jog-direction f!
    0e RMT-jog-velocity f!
;

: RMT-T3-0 true ;

: RMT-T3-1 true ;

\ X, Y, Z 軸 jog 按鍵釋放鍵碼分別為 41, 42, 43
: RMT-T3-2
    RMT-jogging-axis @
    case
        1 of RMT-jog-end-key @ 41 = endof
        2 of RMT-jog-end-key @ 42 = endof
        3 of RMT-jog-end-key @ 43 = endof
        4 of RMT-jog-end-key @ 44 = endof
        5 of RMT-jog-end-key @ 41 = endof
        6 of RMT-jog-end-key @ 42 = endof
    endcase
    RMT-fault @ or
;

: RMT-T3-3 RMT-busy off true ;

step RMT-S3-0
step RMT-S3-1
step RMT-S3-2
step RMT-S3-3

transition RMT-T3-0
transition RMT-T3-1
transition RMT-T3-2
transition RMT-T3-3

' RMT-S3-0 ' RMT-T3-0 -->
' RMT-T3-0 ' RMT-S3-1 -->
' RMT-S3-1 ' RMT-T3-1 -->
' RMT-T3-1 ' RMT-S3-2 -->
' RMT-S3-2 ' RMT-T3-2 -->
' RMT-T3-2 ' RMT-S3-3 -->
' RMT-S3-3 ' RMT-T3-3 -->
' RMT-T3-3 ' RMT-Send -->


\ ===== macro_4 =====
\ 說明：
\   當 T7 transpose 時進入此 macro, 進行自動碰邊量測.
\
\ 流程：
\   1. 根據收到的鍵碼設定對應的命令
\   
\
\ SFC：
\
\ T7             T8
\ +------>S4-0---+--->

\ -----+----------+-------------------------------------------------------------------------------------
\ S4-0 | RMT-S4-0 | 根據收到的建碼設定參數,並啟動edge-meas-go
\ -----+----------+-------------------------------------------------------------------------------------


: RMT-S4-0
    RMT-accepted-key @
    case
        22 of  1 edge-meas-axis ! -10e mm edge-meas-dis f! edge-meas-go on endof 
        23 of  1 edge-meas-axis !  10e mm edge-meas-dis f! edge-meas-go on endof
        26 of  2 edge-meas-axis ! -10e mm edge-meas-dis f! edge-meas-go on endof
        27 of  2 edge-meas-axis !  10e mm edge-meas-dis f! edge-meas-go on endof
        30 of  3 edge-meas-axis ! -10e mm edge-meas-dis f! edge-meas-go on endof
        31 of  3 edge-meas-axis !  10e mm edge-meas-dis f! edge-meas-go on endof
    endcase

    0 17 RMT-led!
    remoter-measure? off
    RMT-busy off
;

step RMT-S4-0
\ ===== SFC Remoter =====
\ 說明：
\   此為主要 SFC ，當 M0 完成初始化工作後由 T0-2 transpose 至此，處理與遙控器通訊之工作，並根據收到的
\   鍵碼 transpose 至各個 macro 處理對應的工作
\
\ 流程：
\
\ SFC：
\
\ RMT-T0-3       T1        T2
\  +--->S1---+---+--->M1---+---+---+
\       ^    |                 |   |
\       |    |   T3        T4  |   |
\       |    +---+--->M2---+---+   |
\       |    |                 |   |
\       |    |   T5        T6  |   |
\       |    +---+--->M3---+---+   |
\       |    |   T7        T8  |   |
\       |    +---+--->M4---+---+   |
\       |                          |
\       +--------------------------+
\
\ ---+--------+-------------------------------------------------------------------------------------
\ S1 | RMT-S1 | 清除 RMT-accepted-key 後，判斷是否有資料在 Input queue 內，若有就拿出一筆資料，判斷是要處理
\    |        | RMT-feed-leds output 還是 Key input，若是 Key input 且需 Double check, 確認收到兩次同
\    |        | 樣的 Key 後, 設定 RMT-accepted-key，否則設定對應的 single-check-key
\ ---+--------+-------------------------------------------------------------------------------------
\
\ ---+--------+-------------------------------------------------------------------------------------
\ T1 | RMT-uncheck-accepted? | 判斷遙控器非忙碌中，且收到對應操作的按鍵
\ ---+--------+-------------------------------------------------------------------------------------
\ T2 | RMT-uncheck-done? | true
\ ---+--------+-------------------------------------------------------------------------------------
\ T3 | RMT-T3 | 判斷遙控器非忙碌中，且收到 OT-release 按鍵，再判斷是否 OT-release 開啟中，或 OT-release 關閉中
\    |        | 但有 X, Y, Z 任一軸 OT，若是就開啟 RMT-busy 旗標並 transpose
\ ---+--------+-------------------------------------------------------------------------------------
\ T4 | RMT-T4 | true
\ ---+--------+-------------------------------------------------------------------------------------
\ T5 | RMT-T5 | 判斷遙控器非忙碌中，且非量測狀態，並收到軸移動按鍵，若是就開啟 RMT-busy 旗標並 transpose
\ ---+--------+-------------------------------------------------------------------------------------
\ T6 | RMT-T6 | true
\ ---+--------+-------------------------------------------------------------------------------------
\ T7 | RMT-T7 | 判斷遙控器非忙碌中，且為量測狀態，並收到移動按鍵，若是就開啟 RMT-busy 旗標並 transpose
\ ---+--------+-------------------------------------------------------------------------------------
\ T8 | RMT-T8 | true
\ ---+--------+-------------------------------------------------------------------------------------
\
\ ---+---------+------------------------------------------------------------------------------------
\ M1 | macro_1 | 處理直接操作且無須等待的工作
\ ---+---------+------------------------------------------------------------------------------------
\ M2 | macro_2 | 處理 OT-release 模式開啟/關閉工作
\ ---+---------+------------------------------------------------------------------------------------
\ M3 | macro_3 | 處理軸移動工作
\ ---+---------+------------------------------------------------------------------------------------
\ M4 | macro_4 | 處理量測工作
\ ---+---------+------------------------------------------------------------------------------------

: RMT-S1
    0 RMT-accepted-key !
    PECM-RMT-ch-slv uart-rx-len@ 0 >            ( flag )
    if                                          ( )
        1 PECM-RMT-ch-slv uart-data@            ( input )
        dup 41 45 within                        ( input flag )
        if                                      ( input )
            RMT-jog-end-key !
        else 
            dup 57 <                            ( input flag )
            if                                  ( input )
                dup RMT-double-check-key @ =    ( input flag )
                if                              ( input )
                    RMT-accepted-key !          ( )
                    0 RMT-double-check-key !
                    0 RMT-jog-end-key !
                else
                    RMT-double-check-key !      ( )
                then
            else
                dup $D9 $DF within              ( input flag )
                if                              ( input )
                    case
                    $D9 of RMT-feed-leds-D9 @ 1 PECM-RMT-ch-slv uart-data! endof
                    $DA of RMT-feed-leds-DA @ 1 PECM-RMT-ch-slv uart-data! endof
                    $DB of RMT-feed-leds-DB @ 1 PECM-RMT-ch-slv uart-data! endof
                    $DC of RMT-feed-leds-DC @ 1 PECM-RMT-ch-slv uart-data! endof
                    $DD of RMT-feed-leds-DD @ 1 PECM-RMT-ch-slv uart-data! endof
                    $DE of RMT-feed-leds-DE @ 1 PECM-RMT-ch-slv uart-data! endof
                    endcase                     ( )
                else
                    0 1 PECM-RMT-ch-slv uart-data!
                    drop                        ( )
                then
            then
        then
        mtime RMT-watchdog !
        RMT-fault off
    then
;


: RMT-T3
    RMT-busy @ not
    RMT-accepted-key @ 10 = and     \ OT-release 鍵碼：10

    RMT-OT-release @
    if
        true
    else
        PECM-has-OT?
    then
    and

    if
        RMT-busy on
        true
    else
        false
    then
;

: RMT-T4 true ;

: RMT-T5
    RMT-busy @ not
    RMT-accepted-key @      \ 軸移動按鍵鍵碼：22,23,26,27,30,31,34,35
    dup 22 =
    over 23 = or
    over 26 = or
    over 27 = or
    over 30 = or
    over 31 = or
    over 34 = or
    swap 35 = or

    remoter-measure? @ not and \ 非量測狀態
    and

    if
        RMT-busy on
        true
    else
        false
    then
;

: RMT-T6 true ;

: RMT-T7
    RMT-busy @ not

    RMT-accepted-key @     \ 移動按鍵鍵碼：22,23,26,27,30,31,34,35
    dup 22 =
    over 23 = or
    over 26 = or
    over 27 = or
    over 30 = or
    over 31 = or
    over 34 = or
    swap 35 = or

    remoter-measure? @  and \ 量測狀態
    and

    if
        RMT-busy on
        true
    else
        false
    then
;

: RMT-T8 true ;

step RMT-S1

transition RMT-T3
transition RMT-T4
transition RMT-T5
transition RMT-T6
transition RMT-T7
transition RMT-T8

' RMT-S1      ' RMT-uncheck-accepted? --> ' RMT-uncheck-accepted? ' RMT-uncheck -->
' RMT-uncheck ' RMT-uncheck-done?     --> ' RMT-uncheck-done?     ' RMT-S1      -->

' RMT-S1 ' RMT-T5 -->
' RMT-T5 ' RMT-S3-0 -->
' RMT-S3-1 ' RMT-T6 -->
' RMT-T6 ' RMT-S1 -->

' RMT-S1 ' RMT-T7 -->
' RMT-T7 ' RMT-S4-0 -->
' RMT-S4-0 ' RMT-T8 -->
' RMT-T8 ' RMT-S1 -->

\ ===== macro_0 =====
\ 說明：
\   此 macro 為 remoter.fs 的入口，進行遙控器初始化工作，然後 transpose 至 RMT-S1
\ 
\ 正常流程：
\   1. 設定 X, Y, Z 軸的 PD-16 為 $0 後，等待至 no waiting requests，然後設定 X, Y, Z 軸的 PD-25 為 $40 後，
\   等待至 no waiting requests，然後等待至 UART ready 後，進行 UART 和遙控器初始設定，然後 transpose 至 RMT-S1
\ 
\ 異常流程：
\   1. 設定完 X, Y, Z 軸的 PD-16 (或 PD-25) 且等待至 no waiting requests 後，若 X, Y, Z 任一軸有 SDO error，
\   就重新設定。
\   2-1. S-watchdog 不斷檢查 RMT-watchdog 和當下時間的差異，若 S1 未正常與遙控器通訊並更新 RMT-watchdog 直到
\   差異 > 100 ms，就設定 RMT-fault = true，並印出異警訊息
\   2-2. T-watchdog 不斷檢查 RMT-watchdog 和當下時間的差異，若 S1 未正常與遙控器通訊並更新 RMT-watchdog 直到
\   差異 > 2000 ms，就 deactivate S1，reset UART，並印出異警訊息
\ 
\ SFC：
\ 
\       T0-0        T0-1        T0-2            T0-3
\ S0-0---+--->S0-1---+--->S0-2---+---+--->S0-3---+---+--->S1
\                                    |               |
\                                    |               |             T-watchdog
\                                    |               +--->S-watchdog---+--->S0-3
\                                    |
\                                    +--->RMT-forth
\                               
\ -----------+----------------+-------------------------------------------------------------------------
\ S0-0       | RMT-S0-0       | 設定 X, Y, Z 軸的 PD-16 為 $0
\ -----------+----------------+-------------------------------------------------------------------------
\ S0-1       | RMT-S0-1       | 設定 X, Y, Z 軸的 PD-25 為 $40
\ -----------+----------------+-------------------------------------------------------------------------
\ S0-2       | RMT-S0-2       | 若 UART ready 就進行 UART 和遙控器初始設定
\ -----------+----------------+-------------------------------------------------------------------------
\ S0-3       | RMT-S0-3       | waiting
\ -----------+----------------+-------------------------------------------------------------------------
\ S-watchdog | RMT-S-watchdog | 檢查 RMT-watchdog 和當下時間的差異，若差異 > 100 ms，就設定 RMT-fault = true，
\            |                | 並印出異警訊息
\ -----------+----------------+-------------------------------------------------------------------------
\
\ -----------+----------------+-------------------------------------------------------------------------
\ T0-0       | RMT-T0-0       | 判斷是否 RMT-S0-0-done = true，且 waiting-requests? = false，且各軸 
\            |                | sdo-error? = false，若是就 transpose，否則不 transpose 並清除 RMT-S0-0-done
\            |                | 旗標使 RMT-S0-0 重新設定 PD-16
\ -----------+----------------+-------------------------------------------------------------------------
\ T0-1       | RMT-T0-1       | 判斷是否 RMT-S0-1-done = true，且 waiting-requests? = false，且各軸
\            |                | sdo-error? = false，若是就 transpose，否則不 transpose 並清除 RMT-S0-1-done
\            |                | 旗標使 RMT-S0-1 重新設定 PD-25
\ -----------+----------------+-------------------------------------------------------------------------
\ T0-2       | RMT-T0-2       | 若 RMT-S0-2-done = true，就 reset UART 並 transpose
\ -----------+----------------+-------------------------------------------------------------------------
\ T0-3       | RMT-T0-3       | 若 UART ready 就更新 RMT-watchdog 並 transpose
\ -----------+----------------+-------------------------------------------------------------------------
\ T-watchdog | RMT-T-watchdog | 檢查 RMT-watchdog 和當下時間的差異，若差異 > 2000 ms，就 deactivate S1
\            |                | reset UART，並印出異警訊息，然後 transpose
\ -----------+----------------+-------------------------------------------------------------------------

variable RMT-S0-0-done
variable RMT-S0-1-done
variable RMT-S0-2-done

: RMT-S0-0
    RMT-S0-0-done @ not
    if
        $0 0 $2310 slave-xa sdo-download-u32
        $0 0 $2310 slave-xb sdo-download-u32
        $0 0 $2310 slave-xc sdo-download-u32
        $0 0 $2310 slave-w sdo-download-u32
        RMT-S0-0-done on
    then
;

\ 這裡先設定好 PD-25 為 $40，後續 OT release 功能就只需要設定 PD-16
: RMT-S0-1
    RMT-S0-1-done @ not
    if
        $40 0 $2319 slave-xa sdo-download-u32
        $40 0 $2319 slave-xb sdo-download-u32
        $40 0 $2319 slave-xc sdo-download-u32
        $40 0 $2310 slave-w sdo-download-u32
        RMT-S0-1-done on
    then
;

: RMT-S0-2
    PECM-RMT-ch-slv uart-ready?
    RMT-S0-2-done @ not and
    if
        6 PECM-RMT-ch-slv uart-baud!
        3 PECM-RMT-ch-slv uart-frame!
        1 32 RMT-led!
        4 RMT-cur-ptr !
        RMT-jog-continue on
        500e mm RMT-jog-distance f!
        RMT-S0-2-done on
    then
;

: RMT-S0-3 ( waiting ) ;

: RMT-S-watchdog
    RMT-fault @ not
    if
        mtime RMT-watchdog @ - 500 >
        if
            RMT-fault on
            PECM-motion-state @ idle =
            if
                touch-ignore off
            then

            system-ready?
            if
                ." error|Remoter not ready.;A1101" cr
            then
        then
    then
;

\ 處理遙控器初始化完成後每個週期都要進行的工作
: RMT-forth ( -- )
    RMT-ffs-forth           \ 處理正反器
;

: RMT-T0-0
    RMT-S0-0-done @
    waiting-requests? not and
    if
        \ slave-x sdo-error? not
        \ slave-y sdo-error? not and
        \ slave-z sdo-error? not and
        true
        if
            true
        else
            RMT-S0-0-done off
            false
        then
    else
        false
    then
;

: RMT-T0-1
    RMT-S0-1-done @
    waiting-requests? not and
    if
        \ slave-x sdo-error? not
        \ slave-y sdo-error? not and
        \ slave-z sdo-error? not and
        true
        if
            true
        else
            RMT-S0-1-done off
            false
        then
    else
        false
    then
;

: RMT-T0-2
    RMT-S0-2-done @
    dup if
        PECM-RMT-ch-slv 0uart
    then
;

: RMT-T0-3
    PECM-RMT-ch-slv uart-ready?
    dup if
        mtime RMT-watchdog !
    then
;

: RMT-T-watchdog
    mtime RMT-watchdog @ - 5000 >
    dup if
        ." log|Remoter trying to recovery." cr
        PECM-RMT-ch-slv 0uart
        ." watchdog "
        ['] RMT-S1 -step
    then
;

step RMT-S0-0
step RMT-S0-1
step RMT-S0-2
step RMT-S0-3
step RMT-S-watchdog
step RMT-forth

transition RMT-T0-0
transition RMT-T0-1
transition RMT-T0-2
transition RMT-T0-3
transition RMT-T-watchdog

' RMT-S0-0 ' RMT-T0-0 -->
' RMT-T0-0 ' RMT-S0-1 -->
' RMT-S0-1 ' RMT-T0-1 -->
' RMT-T0-1 ' RMT-S0-2 -->
' RMT-S0-2 ' RMT-T0-2 -->
' RMT-T0-2 ' RMT-S0-3 -->
' RMT-T0-2 ' RMT-forth -->

' RMT-S0-3 ' RMT-T0-3 -->
' RMT-T0-3 ' RMT-S1 -->
' RMT-T0-3 ' RMT-S-watchdog -->

' RMT-S-watchdog ' RMT-T-watchdog -->
' RMT-T-watchdog ' RMT-S0-3 -->

marker -work