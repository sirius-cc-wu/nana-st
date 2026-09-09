-work

variable m2-pump-targer-b 25000 m2-pump-targer-b !
variable m2-pump-b m2-pump-targer-b @ m2-pump-b !
variable GoEW-step
variable EWcount
variable m2-de-count
variable $buft-run   FALSE $buft-run !
variable $cps-buft-run   FALSE $cps-buft-run !
\ 過濾時間
variable filter-time 300000 filter-time !
\ 逆洗時間
variable backwash-time 15000 backwash-time !

: +m2
  m2-pump-valve -dout
  m2-in-valve-dout +dout
  m2-out-valve-dout +dout
  m2-pump-dout +dout
  m2-pump-targer-b @ m2-pump-aout ec-aout!
;
: -m2
  m2-pump-valve +dout
  m2-in-valve-dout -dout
  m2-out-valve-dout -dout
  0 m2-pump-aout ec-aout!
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
  EA/EB? +dout
  m4-in-valve-dout +dout
  m4-pump-valve +dout
;
: -m4
  m4-pump-dout -dout
  m4-out-valve-dout -dout
  pre-input-valve-dout -dout
  cr6-input-valve-dout -dout
  m4-in-valve-dout -dout
  m4-pump-valve -dout
;
: -m-close
  m2-pump-dout -dout
  m2-pump-valve -dout
  m2-in-valve-dout -dout
  m2-out-valve-dout -dout
  0 m2-pump-aout ec-aout!

  m3-pump-dout -dout

  m4-pump-dout -dout
  m4-out-valve-dout -dout
  pre-input-valve-dout -dout
  cr6-input-valve-dout -dout
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

: +cps-buft-run
  TRUE $cps-buft-run !
;
: -cps-buft-run
  FALSE $cps-buft-run !
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
          EWcount @ filter-time @ > if
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
            m2-pump-aout ec-aout! m2-pump-b !
            m2-de-count @ 5 > if
              1 GoEW-step +!
            then
          then
        endof
        \ 開啟m4前 先開啟EAorEB(沉殿槽輸入閥or六價鉻輸入閥)
        4 of
          EA/EB? +dout
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
          EWcount @ backwash-time @ > if
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
  m2-ready? @ not or
  system-ready? not or
  aqua-off? not and
;
transition aqua-not-ready?

: aqua-ready?
  is-IPC-EMS? is-EW-EMS? or not
  m2-ready? @ and
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


\ aqua porcess
' aqua-idle       ' aqua-off?                 -->
' aqua-idle       ' aqua-not-ready?           -->
' aqua-idle       ' aqua-ready?               -->

' aqua-off?       ' aqua-off-action           -->

\ 可刪除
' aqua-not-ready? ' aqua-idle                 -->

' aqua-ready?       '  aqua-init              -->
' aqua-init         '  aqua-start?            -->
' aqua-start?       '  aqua-go                -->
' aqua-go           '  aqua-stop?             -->
' aqua-stop?        '  aqua-stop-action       -->
' aqua-stop-action  '  aqua-stop-ok?          -->
' aqua-stop-ok?     '  aqua-idle          -->

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


\ 壓濾機 Buffer tank
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
\ | 狀態 | forth 指令       | 說明                  |
\ |------|-----------------|-----------------------|
\ | B0   | cps-bt-init     | buffer tank init      |
\ | B1   | cps-bt-start    | 啟動 buffer tank pump |
\ | B2   | cps-bt-stop     | 關閉 buffer tank pump |
\ 
\ Transition table
\ 
\ | 狀態 | forth 指令   | 說明   |
\ |------|--------------|--------|
\ | BT1  | cps-hl   | 高水位 |
\ | BT2  | cps-midl | 中水位 |
\ | BT3  | cps-lowl    | 低水位 |
\ 

\ Step
: cps-bt-init
  ( do nothing )
;
: cps-bt-start
  +compressor-bt-pump
;
: cps-bt-stop
  -compressor-bt-pump
;

\ Transition
: cps-hl
  \ 上水位
  $cps-buft-run @ if
    compressor-bt-upper@ compressor-bt-lower@ and
  else
    FALSE
  then
;
: cps-midl
  \ 中水位 Ball-1 != Ball-2
  $cps-buft-run @ if
    compressor-bt-upper@ not compressor-bt-lower@ and
    compressor-bt-upper@ compressor-bt-lower@ not and or
  else
    FALSE
  then
;
: cps-lowl
  \ 下水位
  $cps-buft-run @ if
    compressor-bt-upper@ not compressor-bt-lower@ not and
  else
    FALSE
  then
;
: cap-t1
  TRUE
;
: cap-t2
  TRUE
;

\ Step Definition
step cps-bt-init
step cps-bt-start
step cps-bt-stop

\ Transition Definition
transition cps-hl
transition cps-midl
transition cps-lowl
transition cap-t1
transition cap-t2

\ Link
' cps-bt-init       ' cps-hl          -->
' cps-bt-init       ' cps-midl        -->
' cps-bt-init       ' cps-lowl        -->

' cps-hl            ' cps-bt-start    -->
' cps-midl          ' cps-bt-init     -->
' cps-lowl          ' cps-bt-stop     -->
' cps-bt-start      ' cap-t1          -->
' cps-bt-stop       ' cap-t2          -->

' cap-t1            ' cps-bt-init     -->
' cap-t2            ' cps-bt-init     -->

marker -work
