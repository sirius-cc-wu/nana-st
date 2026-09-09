-work

variable m2-pump-targer-b 25000 m2-pump-targer-b !
variable m2-pump-b m2-pump-targer-b @ m2-pump-b !
variable GoEW-step
variable EWcount
variable m2-de-count
variable $buft-run   FALSE $buft-run !
variable $NaN03-pump FALSE $NaN03-pump !

: +m2
  m2-pump-valve -dout
  m2-in-valve-dout +dout
  m2-out-valve-dout +dout
  m2-pump-dout +dout
  \ m2-pump-targer-b @ m2-pump-aout ec-aout!
;
: -m2
  m2-pump-valve +dout
  m2-in-valve-dout -dout
  m2-out-valve-dout -dout
  \ 0 m2-pump-aout ec-aout!
;
: +m3
  m3-pump-dout +dout
;
: -m3
  m3-pump-dout -dout
;
: +m4
  m4-pump-dout +dout
  m4-out-valve-dout +dout
  pre-input-valve-dout +dout
  m4-in-valve-dout +dout
  m4-pump-valve +dout
;
: -m4
  m4-pump-dout -dout
  m4-out-valve-dout -dout
  pre-input-valve-dout -dout
  m4-in-valve-dout -dout
  m4-pump-valve -dout
;
: -m-close
  m2-pump-dout -dout
  m2-pump-valve -dout
  m2-in-valve-dout -dout
  m2-out-valve-dout -dout
  \ 0 m2-pump-aout ec-aout!

  m3-pump-dout -dout

  m4-pump-dout -dout
  m4-out-valve-dout -dout
  pre-input-valve-dout -dout
  m4-in-valve-dout -dout
  m4-pump-valve -dout
;

\ Bufer Tank start/stop command
: +buft-run
  TRUE $buft-run !
;
: -buft-run
  FALSE $buft-run !
;

\ aqua sfc
\ Step Activity
: aqua-idle
  ( wait )
;
step aqua-idle

: aqua-init
  1 GoEW-step !
  0 EWcount !
  +g-led
;
step aqua-init

: aqua-go
  GoEW-step @
    case
        \ 開啟m3 m2與關閉m4
        1 of
          +m2
          +m3
          -m4
          1 GoEW-step +!
          0 EWcount !
          m2-pump-targer-b @ m2-pump-b !
        endof

        \ 等待數分鐘 開啟m2數分鐘後 關閉m2並開啟m4
        2 of
          1 EWcount +!
          EWcount @ 300000 > if
            1 GoEW-step +!
            0 m2-de-count !
            0 EWcount !
          then
        endof
        
        \ 完全關閉m2前 先每三秒降一次轉速
        3 of
          1 EWcount +!
          EWcount @ 3000 > if
            0 EWcount !
            1 m2-de-count +!
            m2-pump-b @ 2 / dup
            \ m2-pump-aout ec-aout! m2-pump-b !
            m2-de-count @ 5 > if
              1 GoEW-step +!
            then
          then
        endof
        \ 開啟m4前 先開啟4Y13(沉殿槽輸入閥)
        4 of
          pre-input-valve-dout +dout
          1 GoEW-step +!
          0 EWcount !
        endof
        \ 等待3秒後 在開啟m4
        5 of
          1 EWcount +!
          EWcount @ 3000 > if
            1 GoEW-step +!
          then
        endof
        \ 如果m4 ready, 關閉m2並開啟m4
        6 of
          m4-ready? @ if
            -m2 
            +m4
            1 GoEW-step +!
            0 EWcount !
          else
            8 GoEW-step !
          then
        endof
        \ 等待數分鐘 開啟m4數秒鐘後 先關閉m4 pump在重回step1
        7 of
          1 EWcount +!
          EWcount @ 15000 > if
            1 GoEW-step +!
          then
        endof
        \ 先關閉m4 pump
        8 of
          0 EWcount !
          m4-pump-dout -dout
          1 GoEW-step +!
        endof
        \ 等待數秒後 在重回step1
        9 of
          1 EWcount +!
          EWcount @ 5000 > if
            1 GoEW-step !
          then
        endof
    endcase
;
step aqua-go

: aqua-off-action
  -m-close
  +y-led
;
step aqua-off-action

: aqua-stop-action
  -m-close
  +y-led
;
step aqua-stop-action

\ ==================== transitions ====================
: aqua-off?
  check-sys-off?
;
transition aqua-off?

: aqua-not-ready?
  is-IPC-EMS? is-EW-EMS? or
  \ m2-ready? @ not or
  system-ready? not or
  aqua-off? not and
;
transition aqua-not-ready?

: aqua-ready?
  is-IPC-EMS? is-EW-EMS? or not
  \ m2-ready? @ and
  system-ready?  and
  aqua-off? not and
;
transition aqua-ready?

: aqua-stop?
  aqua-not-ready? check-sys-off? or
;
transition aqua-stop?

: aqua-start?
  true
;
transition aqua-start?
: aqua-stop-ok?
  true
;
transition aqua-stop-ok?


\ \ aqua porcess
\ ' aqua-idle       ' aqua-off?                 -->
\ ' aqua-idle       ' aqua-not-ready?           -->
\ ' aqua-idle       ' aqua-ready?               -->
\ 
\ ' aqua-off?       ' aqua-off-action           -->
\ 
\ \ 可刪除
\ ' aqua-not-ready? ' aqua-idle                 -->
\ 
\ ' aqua-ready?       '  aqua-init              -->
\ ' aqua-init         '  aqua-start?            -->
\ ' aqua-start?       '  aqua-go                -->
\ ' aqua-go           '  aqua-stop?             -->
\ ' aqua-stop?        '  aqua-stop-action       -->
\ ' aqua-stop-action  '  aqua-stop-ok?          -->
\ ' aqua-stop-ok?     '  aqua-idle          -->

\ Buffer tank
\ 
\ 說明:
\ 
\ 正常流程:
\ 
\ a1: 偵測上與下的浮球訊號, 若皆為 TRUE 為高水位, 啟動 Buffer Tank。
\ a2: 偵測上與下的浮球訊號, 若皆為 FALSE 為低水位, 關閉 Buffer Tank。
\ a3: 偵測上與下的浮球訊號, 若上浮球為 FALSE, 下浮球為 TRUE, 為中水位, 不執行任何
\     動作。
\ 
\ 
\ 異常流程:
\ 
\ b1: 偵測上與下的浮球訊號, 若上浮球為 TRUE, 下浮球為 FALSE, 為浮球訊號異常, 執行
\     浮球異常異警。(TODO: 目前這個是與 a3 執行動作相同，未發異警。)
\ 
\ 
\ SFC 邏輯圖:
\ 
\            BT1 +----+ TRUE
\          +--+--+ B1 +--+---> B0
\          |     +----+
\ +----+   | BT2
\ | B0 +---+--+--> B0
\ +----+   |
\          | BT3 +----+ TRUE
\          +--+--+ B2 +--+---> B0
\                +----+
\ 
\ Step table
\ 
\ | 狀態 | forth 指令      | 說明                  |
\ |------|-----------------|-----------------------|
\ | B0   | buft-init       | buffer tank init      |
\ | B1   | buft-pump-start | 啟動 buffer tank pump |
\ | B2   | buft-pump-stop  | 關閉 buffer tank pump |
\ 
\ Transition table
\ 
\ | 狀態 | forth 指令   | 說明   |
\ |------|--------------|--------|
\ | BT1  | high-level   | 高水位 |
\ | BT2  | middle-level | 中水位 |
\ | BT3  | low-level    | 低水位 |
\ 

\ Step
: buft-init
  ( do nothing )
;
: buft-pump-start
  +aqua-bt-pump
;
: buft-pump-stop
  -aqua-bt-pump
;

variable upper-water FALSE upper-water !
variable lower-water TRUE lower-water !

\ Transition
: high-level
  \ 上水位
  $buft-run @ if
    aqua-bt-upper@ aqua-bt-lower@ and
  else
    FALSE
  then
;
: middle-level
  \ 中水位 Ball-1 != Ball-2
  $buft-run @ if
    aqua-bt-upper@ not aqua-bt-lower@ and
    aqua-bt-upper@ aqua-bt-lower@ not and or
  else
    FALSE
  then
;
: low-level
  \ 下水位
  $buft-run @ if
    aqua-bt-upper@ not aqua-bt-lower@ not and
  else
    FALSE
  then
;
: aqua-t19
  TRUE
;
: aqua-t20
  TRUE
;

\ Step Definition
step buft-init
step buft-pump-start
step buft-pump-stop

\ Transition Definition
transition high-level
transition middle-level
transition low-level
transition aqua-t19
transition aqua-t20

\ Link
' buft-init       ' high-level        -->
' buft-init       ' middle-level      -->
' buft-init       ' low-level         -->

' high-level      ' buft-pump-start   -->
' middle-level    ' buft-init         -->
' low-level       ' buft-pump-stop    -->
' buft-pump-start ' aqua-t19          -->
' buft-pump-stop  ' aqua-t20          -->

' aqua-t19        ' buft-init         -->
' aqua-t20        ' buft-init         -->

\ 
\ ========================NaNO3加藥幫浦===================
\
variable Nacl-pump-timer 0 Nacl-pump-timer !
\ Step
: NaN03-pump-init
  ( do nothing )
;
: NaN03-pump-start
  NaN03-pump-aout ec-aout! \ 待補充aout數值
;
: NaN03-pump-stop
  NaN03-pump-aout ec-aout! \ 待補充aout數值
;
: NaCl-pump-start
  NaCl-sw-valve-dout +dout
;
: NaCl-pump-close
  NaCl-sw-valve-dout -dout
;
: NaCl-pump-complete?
  NaCl-pump-timer @ NaCl-pump-time @ >
  if
    0 NaCl-pump-timer !
    TRUE
  else
    1 Nacl-pump-timer +!
    FALSE
  then
;
: +NaN03-pump-run
  TRUE $NaN03-pump !
;
: -NaN03-pump-run
  FALSE $NaN03-pump !
;

: NaN03-err?
  NaN03-err-din ec-din@ not
;
\ Transition
: conductivit-high-level
  \ 上導電度
  $NaN03-pump @ if
    NaN03-err? if
      FALSE
    else
      reactor-conduct@ cond-high-limit f@ f>
    then
  else
    FALSE
  then
;
: conductivit-middle-level
  \ 中導電度
  $NaN03-pump @ if
    NaN03-err? if
      FALSE
    else
      reactor-conduct@ cond-high-limit f@ f< reactor-conduct@ cond-low-limit f@ f> and
    then
  else
    FALSE
  then
;
: conductivit-low-level
  \ 下導電度
  $NaN03-pump @ if
    NaN03-err? if
      FALSE
    else
    reactor-conduct@ cond-low-limit f@ f<
    then
  else
    FALSE
  then
;
: NaN03-pump-err
  NaN03-err?
;
: NaN03-pump-t18
  TRUE
;
: NaN03-pump-t19
  TRUE
;
: NaN03-pump-t20
  TRUE
;

\ Step Definition
step NaN03-pump-init
step NaN03-pump-start
step NaN03-pump-stop
step NaCl-pump-start
step NaCl-pump-close

\ Transition Definition
transition conductivit-high-level
transition conductivit-middle-level
transition conductivit-low-level
transition NaCl-pump-complete?
transition NaN03-pump-err
transition NaN03-pump-t18
transition NaN03-pump-t19
transition NaN03-pump-t20

\ Link
' NaN03-pump-init            ' conductivit-high-level    -->
' NaN03-pump-init            ' conductivit-middle-level  -->
' NaN03-pump-init            ' conductivit-low-level     -->
' NaN03-pump-init            ' NaN03-pump-err            -->

' conductivit-low-level       ' NaN03-pump-start         -->
' conductivit-middle-level    ' NaN03-pump-init          -->
' conductivit-high-level      ' NaN03-pump-stop          -->
' NaN03-pump-err             ' NaCl-pump-start          -->
' NaCl-pump-start            ' NaCl-pump-complete?      -->
' NaCl-pump-complete?        ' NaCl-pump-close          -->
' NaCl-pump-close            ' NaN03-pump-t18           -->

' NaN03-pump-start           ' NaN03-pump-t19           -->
' NaN03-pump-stop            ' NaN03-pump-t20           -->

' NaN03-pump-t18             ' NaN03-pump-init          -->
' NaN03-pump-t19             ' NaN03-pump-init          -->
' NaN03-pump-t20             ' NaN03-pump-init          -->


\ washback and filter
\
\ a1: 超過水位上限時，調整filter-washback旗標，使過濾+逆洗流程開始運作，直到水位低於下限。
\     過濾+逆洗 = 開啟客戶端，開啟過濾開關，三分鐘後關閉過濾開關，開啟逆洗開關，30秒後關閉逆洗開關。
\ a2: 中水位時，維持目前的旗標狀態
\ a3: 低水位時，放下旗標
\
\ SFC 邏輯圖:
\ 
\                                          BT4   +----+  BT6
\     BT3                              +--+--+---+ B4 +---+----+
\    +---+                             |         +----+        |
\    |   |                             |                       |
\ +--+-+ |  BT2  +----+ TRUE  +----+   |                       |   +----+ TRUE 
\ | B0 +----+----+ B2 +---+---+ B3 +---+                       +---+ B5 +--+---> B0
\ +----+ |       +----+       +----+   |                       |   +----+
\        |BT1                          |                       |
\     +--+-+                           |       BT5             |
\     | B1 |                           +-----------+-----------+
\     +--+-+                                                
\     
\
\ Step table
\ 
\ | 狀態 | forth 指令                      | 說明                              |
\ |------|--------------------------------|-----------------------------------|
\ | B0   | aqua-idle                      | aqua-idle                         |
\ | B1   | filter-washback-off-action     | 關閉                              |
\ | B2   | filter-washback-init           | filter-washback-init              |
\ | B3   | check-flag                     | 檢查狀態並改變旗標狀態              |
\ | B4   | filter-washback-state-machine  | 過濾逆洗程序                       |
\ | B5   | filter-off-washback-off        | 關閉 filter 開關並關閉 washback開關 |
\
\ Transition table
\ 
\ | 狀態 | forth 指令                          | 說明                                |
\ |------|------------------------------------|-------------------------------------|
\ | BT1  | aqua-off?                          | 是否off                             |
\ | BT2  | aqua-ready?                        | 是否ready                           |
\ | BT3  | aqua-not-ready?                    | 是否not-ready                       |
\ | BT4  | flag-high?                         | 旗標是否舉起                         |
\ | BT5  | flag-low?                          | 旗標是否放下                         |
\ | BT6  | filter-washback-complete-or-stop?  | 過濾逆洗程序是否完成或是是否有stop訊號 |

variable filter-washback-water-flag FALSE filter-washback-water-flag !
variable filter-timer 0 filter-timer !
variable washback-timer 0 washback-timer !
variable filter-washback-state 1 filter-washback-state !

: +client+filter-washback
  client-on-off-dout +dout
  filter-on-off-dout +dout
  backwash-on-off-dout -dout
  m2-pump-valve +dout
;

: +client-filter+washback
  client-on-off-dout +dout
  filter-on-off-dout -dout
  backwash-on-off-dout +dout
  pre-input-valve-dout +dout
  m2-pump-valve -dout
;

: -client-filter-washback
  client-on-off-dout -dout
  filter-on-off-dout -dout
  backwash-on-off-dout -dout
  pre-input-valve-dout -dout
;

: +cycle-water-low
  cycle-water-low-dout +dout
;

: -cycle-water-low
  cycle-water-low-dout -dout
;

\ B0
: filter-washback-init
 ( do nothing )
;

\ B1
: check-flag 
  filter-wahsback-high-level@ filter-washback-low-level@ and 
  if
    TRUE filter-washback-water-flag !
  else
    filter-wahsback-high-level@ not filter-washback-low-level@ not and
    if
      FALSE filter-washback-water-flag !
    else
      filter-washback-water-flag @ filter-washback-water-flag !
    then
  then
;

\ B2
: filter-start-washback-off 
  +client+filter-washback
;

\ B3
: filter-off-washback-start 
  +client-filter+washback
;

\ B4
: filter-off-washback-off
  -client-filter-washback
;

: filter-washback-stop-action
  FALSE filter-washback-water-flag !
  filter-off-washback-off
;

: filter-washback-off-action
  FALSE filter-washback-water-flag !
  filter-off-washback-off
;

: filter-washback-state-machine
  filter-washback-state @
  case
    1 of
      filter-start-washback-off 
      filter-timer @ filter-time @ > filter-washback-low-level@ not or
      if
        filter-timer @ filter-time @ > not
        if
          FALSE filter-washback-water-flag !
        else
        then
        2 filter-washback-state !
        0 filter-timer !
      else
        1 filter-timer +!
      then
    endof
    2 of
      filter-off-washback-start 
      washback-timer @ washback-time @ >
      if
        3 filter-washback-state !
        0 washback-timer !
      else
        1 washback-timer +!
      then
    endof
    3 of
      filter-off-washback-off
      4 filter-washback-state !
    endof
    4 of
      ( complete )
    endof
  endcase      
;

\ ======================transition===================

: filter-washback-complete-or-stop?
  filter-washback-state @ 4 = aqua-stop? or
  if
    1 filter-washback-state !
    0 filter-timer !
    0 washback-timer !
    FALSE filter-washback-water-flag !
    TRUE
  else
    FALSE
  then
;

\ BT1
: flag-high?
  filter-washback-water-flag @
;

\ BT2
: flag-low?
  filter-washback-water-flag @ not
;

: filter-washback-t19
  TRUE
;

: filter-washback-t20
  TRUE
;

\ Step Definition
step filter-washback-init
step check-flag 
step filter-off-washback-off
step filter-washback-off-action
step filter-washback-state-machine

\ Transition Definition
transition flag-high?
transition flag-low?
transition filter-washback-t19
transition filter-washback-t20
transition filter-washback-complete-or-stop?



\ aqua porcess
' aqua-idle                   ' aqua-off?                   -->
' aqua-idle                   ' aqua-not-ready?             -->
' aqua-idle                   ' aqua-ready?                 -->

' aqua-off?                   ' filter-washback-off-action  -->

' aqua-not-ready?             ' aqua-idle                   -->

' aqua-ready?                 ' filter-washback-init        -->
' filter-washback-init        ' filter-washback-t19         -->
' filter-washback-t19         ' check-flag                  -->

' check-flag                  ' flag-high?                  -->
' check-flag                  ' flag-low?                   -->

' flag-high?                         ' filter-washback-state-machine      -->
' filter-washback-state-machine      ' filter-washback-complete-or-stop?  -->
' filter-washback-complete-or-stop?  ' filter-off-washback-off            -->

' flag-low?                   ' filter-off-washback-off     -->
' filter-off-washback-off     ' filter-washback-t20         -->
' filter-washback-t20         ' aqua-idle                   -->



\ 還需要一個sfc 監視水位下限並調整cycle-water-low-dout(循環槽液位下限輸出)
: cycle-water-low-digital-out
  filter-washback-low-level@ not 
  if
    cycle-water-low-dout +dout
  else
    cycle-water-low-dout -dout
  then
;
step cycle-water-low-digital-out

: cycle-water-low-digital-out-t01
  TRUE
;
transition cycle-water-low-digital-out-t01

' cycle-water-low-digital-out ' cycle-water-low-digital-out-t01 -->
' cycle-water-low-digital-out-t01 ' cycle-water-low-digital-out -->

marker -work
