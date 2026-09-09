-work

variable $peripherals-err
variable $drive-state
variable $sys-off-state FALSE $sys-off-state !
variable $pw-btn-state FALSE $pw-btn-state !

\ Button variable
variable $btn1
variable $btn2
variable $btn3
variable $btn4
: btn-toggle? ( -- flag )
  $btn3 @ $btn4 !
  $btn2 @ $btn3 !
  $btn1 @ $btn2 !
  pw-btn@ 0<> $btn1 !
  $btn1 @ $btn2 @ $btn3 @ not $btn4 @ not and and and $pw-btn-state !
;

\ System Button variable
variable $sys-btn1
variable $sys-btn2
variable $sys-btn3
variable $sys-btn4

: sys-off-btn? ( -- flag )
  $sys-btn3 @ $sys-btn4 !
  $sys-btn2 @ $sys-btn3 !
  $sys-btn1 @ $sys-btn2 !
  sys-off-btn@ 0<> $sys-btn1 !
  $sys-btn1 @ $sys-btn2 @ $sys-btn3 @ not $sys-btn4 @ not and and and $sys-off-state !
;


variable $force-3phase   FALSE $force-3phase !

: force-3phase! ( flag -- ) $force-3phase ! ;

\ 確認三相電源
: 3phase-err? ( -- flag )
  3phase@ not $force-3phase @ or
;

: ems-err? ( -- flag )
  is-EMS?
;

\ 給使用者選擇是否忽略此檢查
variable ignore-power-door
: +ignore-power-door
  ignore-power-door on
;

: -ignore-power-door
  ignore-power-door off
;

\ 檢查PEM門是否關閉
: power-door-close? ( -- flag )
  power-door ec-din@
  ignore-power-door @
  or
;

variable v-press-err
variable v-flow-err
fvariable v-press-threshold \ 單位: bar
fvariable v-flow-threshold  \ 單位: L/min

\ 取得壓力異警
: .v-press-err ." v_press_err|" v-press-err @ 0 .r cr ;
\ 取得流量異警
: .v-flow-err ." v_flow_err|" v-flow-err @ 0 .r cr ;

\ 確認 V 軸壓力感測器
: check-v-press
  v-press-threshold f@ f0= if
    \ 不檢查壓力值
  else
    v-press@ v-press-threshold f@ f< if
      1 v-press-err !
    else
      0 v-press-err !
    then
  then
;

\ 確認 V 軸流量計
: check-v-flow
  v-flow-threshold f@ f0= if
    \ 不檢查流量值
  else
    v-flow@ v-flow-threshold f@ f< if
      1 v-flow-err !
    else
      0 v-flow-err !
    then
  then
;

: v-serr? ( -- flag )
  check-v-press v-press-err @ 0<>
  check-v-flow v-flow-err @ 0<> or
;

create h2-l1 falign 0e f, 30e f, 30e f, \ <0, Platform_L1, Buffer_Tank_L1>
create h2-l2 falign 0e f, 50e f, 50e f, \ <0, Platform_L2, Buffer_Tank_L2>
create h2-v falign 3 floats allot \ <0, Platform_value, Buffer_Tank_value>

\ 紀錄氫氣感測器等級
\ intex: <0, Platform_value, Buffer_Tank_value>
\ states: 0: Nomal, 1: Warn, 2: Danger
create h2-level 3 cells allot
\ 取得加工平台氫氣等級
: .platform-h2-level ." platform_h2|" 1 h2-level param@ 0 .r cr ;
\ 取得 Buffer Tank 氫氣等級
: .bft-h2-level      ." bft_h2|"      2 h2-level param@ 0 .r cr ;

\ 檢查氫爆感測器
\ 目前安裝兩台氫爆感測器，一台在加工平台，一台在 Buffer Tank
: check-h2
  platform-h2@ 1 h2-v fparam!
  bft-h2@      2 h2-v fparam!
  3 1 do
    i h2-l1 fparam@ i h2-l2 fparam@ f> if
      \ 等級數值範圍錯誤
      3 i h2-level param!
    else
      i h2-l1 fparam@ f0= i h2-l2 fparam@ f0= and if
        \ 不檢查氫爆感測器
        0 i h2-level param!
      else
        i h2-v fparam@ i h2-l2 fparam@ f> if
          2 i h2-level param!
        else
          i h2-v fparam@ i h2-l1 fparam@ f> if
            1 i h2-level param!
          else
            0 i h2-level param!
          then
        then
      then
    then
  loop
;


5 constant axes-len
create min-ferr-limit falign axes-len floats allot \ index: <_, x, y, z, v>
create max-ferr-limit falign axes-len floats allot \ index: <_, x, y, z, v>
variable $force-ferr
variable ferr-axis

\ 使用靜止或是運動中的 following error limit
: ferr-limit ( n -- ) ( F: -- limit )
    dup axis-rest? $drive-state @ not or if
      min-ferr-limit
    else
      max-ferr-limit
    then
    fparam@
;

\ 檢查 following error
: check-ferr
  0 ferr-axis !
  \ 不檢查v軸
  axes-len 1 - 1 do
    i axis-ferr@ fabs i ferr-limit f> $force-ferr @ or if
      \ 使用位元數紀錄有 following errorr 的軸。Bit: v, z, y, x。
      i ferr-axis !
      \ reset axis following error
      $drive-state @ not if
        i 0axis-ferr
      then
    then
  loop
;
\ 是否有 following error
: ferr? ( -- flag ) check-ferr ferr-axis @ 0<> ;

4 constant enc-len
\ 雙迴授門檻值
create clerr-threshold falign axes-len floats allot
\ 紀錄光學尺或是有雙迴授誤差過大的軸
create clerr-axes enc-len cells allot

: clerr-limit! ( alix-no -- ) ( F: limit -- )
  dup fdup
  \ 設定雙回授控制的最大補償量
  2e f* max-pos-dev!
  \ 設定雙回授門檻值
  clerr-threshold fparam!
;

\ 檢查 x, y, z 軸的光學尺狀態與雙迴授值
: check-clerr
  \ clerr-axes 清為0
  enc-len 1 do
    0 i clerr-axes param!
  loop
  enc-len 1 do
    i axis-ext-enc@ ec-enc-ready not if
      \ 光學尺未備妥
        4 i clerr-axes param!
    else
      i axis-ext-enc@ ec-enc-err if
        \ 光學尺異警
        2 i clerr-axes param!
      else
        i axis-clerr fabs i clerr-threshold fparam@ f> if
          \ 紀錄雙迴授誤差過大的軸
          1 i clerr-axes param!
        then
      then
    then
  loop
;

\ 光學尺或雙迴授誤差過大?
: clerr? ( -- flag)
  check-clerr
  enc-len 1 do
    i clerr-axes param@ 0<>
  loop
  or or
;

: all-drive-ready?
  \ 檢查所有軸是否 drive on 成功
  drive-x drive-on? drive-y drive-on?
  drive-z drive-on? drive-v drive-on?
  and and and if
    TRUE
  else
    FALSE
  then
;

: .pw-on-error-msg
  \ 三相電源異常
  3phase-err? if
    ." error|Three phase detector error.;A1102" cr
  then
  \ V 軸壓力與流量異警
  v-press-err @ if
    ." error|V axis Press too small.;A1103" cr
  then
  v-flow-err @ if
    ." error|V axis Flow too small.;A1104" cr
  then
  \ axis ferr
  ferr-axis @ 0<> if
    ." error|Axis " ferr-axis @ 0 .r ." : Following error too large;A1105" cr
  then

  power-door-close? not if
    ." error|PEM door not closed.;A1106" cr
  then

  \ 氫爆異警
  1 h2-level param@ 
  case
    2 of ." error|h2 of Platform in Level 2;A1107" cr endof
    3 of ." error|h2 of Platform value Level-1 is more than Level-2;A1108" cr endof
  endcase
  2 h2-level param@
  case
    2 of  ." error|h2 of Buffer Tank in Level 2;A1109" cr endof
    3 of  ." error|h2 of Buffer value Level-1 is more than Level-2;A1110" cr endof
  endcase

  \ 光學尺或雙迴授誤差過大異警
  enc-len 1 do
    i clerr-axes param@
    case
      \ 1:雙回授誤差過大
      1 of  ." error|Axis " i 0 .r ." : Encoder diff too large. ;A1111" cr endof
      2 of  ." error|Axis " i 0 .r ." : Encoder error code: " i axis-ext-enc@ ec-enc-err 0 .r ." ;A1112" cr endof
      4 of  ." error|Axis " i 0 .r ." : Encoder not ready. ;A1113" cr endof
    endcase
  loop
;
: check-drive-on-peripherals
  
  \ 檢查三相電源
  3phase-err?  
  \ 檢查緊急開關
  ems-err?   
  \ 檢查氣壓軸
  v-serr?   
  \ 檢查 Following error
  ferr?   
  \ 檢查氫氣為等級2
  check-h2
  1 h2-level param@ 1 >
  2 h2-level param@ 1 > or 
  \ 光學尺或雙迴授誤差過大
  clerr?   
  \ 檢查PEM門是否關閉
  power-door-close? not
  or or or or or or $peripherals-err !
  $peripherals-err @ if
    .pw-on-error-msg
  then
;

: check-pw-on-peripherals
  check-drive-on-peripherals
  \ 檢查 following-error 過大
  drive-x drive-fault? if
    ." drive-fault|x" cr
    \ 週邊異常旗標
    TRUE $peripherals-err !
  then
  drive-y drive-fault? if
    ." drive-fault|y" cr
    \ 週邊異常旗標
    TRUE $peripherals-err !
  then
  drive-z drive-fault? if
    ." drive-fault|z" cr
    \ 週邊異常旗標
    TRUE $peripherals-err !
  then
  drive-v drive-fault? if
    ." drive-fault|v" cr
    \ 週邊異常旗標
    TRUE $peripherals-err !
  then
;






\ ------------------------------------------------------------------------------
\ Power-On/Off
\ ------------------------------------------------------------------------------
\ 
\ 這裡為簡單說明 power on/off 的流程與 SFC 圖表。
\ 
\ 
\ 說明:
\ 
\ 1. 正常流程:
\ 
\   1.1: CNC 後備妥後, 進行 already-power-off(S1), 按下 power btn, 進行 power-on
\        (S2) 程序, 確認週邊有無異常, 進行 drive-on(M1) 程序。
\   1.2: 已完成 drvie-on(M1) 後, 持續進行確認週邊有無異常。
\   1.3: 若再次按下 power btn, 進行 drive-off 程序(M2), 完成 drive-off 程序後,
\        進入 already-power-off(S1)。
\ 
\ 2. 異常流程:
\   2.1: 若 (1.1) 週邊有異常, 則發送該異常對應之異警, 並進入 drive-off 程序, 回
\        到 already-power-off 狀態。
\   2.2: 若 (1.1) 進行 drive-on 失敗, 則發送 drive-on-err 異警, 並進入 drive-off
\        程序, 回到 already-power-off 狀態。
\   2.3:
\ 
\ 
\ Graphics:
\ 
\ ```
\ +----+ T1 +----+   T2 ++----++ T3 +----+ T4 ++----++ T5
\ | S0 +-+--+ S1 |-+-+--|| M1 ||-+--+ S2 |-+--|| M2 ||-+--> S0
\ +----+    +----+ |    ++----++    +----+    ++----++
\                  |
\                  |
\                  |
\                  | !T2
\                  +--+----> M2
\ ```
\ 
\ Setp and Transition table:
\ 
\ Step
\ 
\ | 狀態  | forth 指令        | 說明                                   |
\ |-------|-------------------|----------------------------------------|
\ | S0    | already-pw-off    | power off 狀態                         |
\ | S1    | pw-on-process     | 週邊檢查                               |
\ | S2    | already-pw-on     | power on 狀態                          |
\ | M1    | drive-on-process  | Drive-on 程序                          |
\ | M2    | drive-off-process | Drive-off 程序                         |
\ 
\ Transition
\ 
\ | 狀態 | forth 指令        | 說明                                       |
\ |------|-------------------|--------------------------------------------|
\ | T1   | on-btn?           | 按下 Power on 鈕                           |
\ | T2   | into-drive-on?    | 是否進入 Drive on                          |
\ | !T2  | peripherals-err?  | 週邊異常                                   |
\ | T3   | all-drive-on?     | Drive on 成功                              |
\ | T4   | into-drive-off?   | 週邊異常或按下 Power off 鈕                |
\ | T5   | all-drive-off?    | Drive off 成功                             |
\ ------------------------------------------------------------------------------


\ Step Activity
: already-pw-off
;

: already-pw-on
  TRUE ECM-power-state !
  1 mdu-on!
  +pw-led
  +pem
  check-pw-on-peripherals
  move-v-count @ 0 = if
    1 move-v-count !
  then
;

: pw-on-process
  check-drive-on-peripherals
;

: drive-on-process
 \ 驅動器送電
  +pw-supply-on
;

: drive-off-process
  \ FALSE $peripherals-err !
  FALSE ECM-power-state !
  0 move-v-count !
  0 mdu-on!
  FALSE $drive-state !
  -pw-led
  -pem
;


\ Transition Activity
: check-pw-btn
  btn-toggle? 
  $pw-btn-state @
  system-ready? and
  if
    TRUE
  else
    FALSE
  then
;

: into-drive-off?
  btn-toggle?
  sys-off-btn?
  
  $peripherals-err @
  $pw-btn-state @
  $sys-off-state @
  or or if
    TRUE
  else
    FALSE
  then

;

: peripherals-err?
  $peripherals-err @
;

: into-drive-on?
  peripherals-err? not
;

: all-drive-on?
  $drive-state @
;

: all-drive-off?
  $drive-state @  not
;


\ Step Definition
step already-pw-off
step already-pw-on
step drive-on-process
step drive-off-process
step pw-on-process


\ Transition Definition
transition peripherals-err?
transition into-drive-on?
transition all-drive-on?
transition into-drive-off?
transition all-drive-off?
transition check-pw-btn


\ Link
\ 初始 power on, 週邊有異常
' already-pw-off       ' check-pw-btn   -->
' check-pw-btn         ' pw-on-process  -->
' pw-on-process        ' peripherals-err?   -->
' peripherals-err?     ' drive-off-process  -->
\ 進入 drive on 程序
' pw-on-process       ' into-drive-on?  -->
' into-drive-on?      ' drive-on-process   -->
\ power on 中, 週邊有異常或按下 power off
' already-pw-on       ' into-drive-off?    -->
' into-drive-off?     ' drive-off-process  -->




\ ------------------------------------------------------------------------------
\ drive off process
\ ------------------------------------------------------------------------------
\ 說明:
\ 
\  關閉aqua (M4), 各軸 drive-off (M5), 關閉 I/O (M6), 關閉並記錄閥門與 
\ deionized-pump (M7), power-on 燈滅,
\ 
\ Graphics:
\ 
\ ```
\  T1-M1 +------+ T1-1 +------+ T1-M4 ++----++ T1-M5 ++----++ T1-M6 ++----++
\ --+----| M1-0 |--+---| M1-1 |--+----|| M4 ||--+----|| M5 ||--+----|| M6 ||---+
\        +------+      +------+       ++----++       ++----++       ++----++   |
\                                                                              |
\ +----------------------------------------------------------------------------+
\ | T1-M7 ++----++ T1-M8 ++----++
\ +--+----|| M7 ||--+----|| M8 ||
\         ++----++       ++----++
\ ```
\ M4: aqua off
\ 
\ ```
\  T1-M4 +------+ T1-M5
\ --+----| M4-0 |--+--->
\        +------+
\ 
\ ```
\ 
\ M5: Drive off
\ 
\ ```
\  T1-M5 +------+  T1M5-3 +------+ T1M5-4 +------+ T1M5-5 +------+
\ --+----| M5-0 |---+-----| M5-3 |--+-----| M5-4 |--+-----| M5-5 |----+
\        +------+         +------+        +------+        +------+    |
\ +-------------------------------------------------------------------+
\ |
\ | T1M5-6 +------+ T1M5-7 +------+  T1M5-8 +------+ T1M5-9 +------+
\ +--+-----| M5-6 |--+-----| M5-7 |---+-----| M5-8 |--+-----| M5-9 |--+
\          +------+        +------+         +------+        +------+  |
\ +-------------------------------------------------------------------+
\ |
\ | T1M5-10 +-------+  T1-M6
\ +--+------| M5-10 |---+---> M6
\           +-------+
\ 
\ ```
\ 
\ M6: I/O off
\ 
\ ```
\  T1-M6 +------+ T1-M7
\ --+----| M6-0 |--+--->
\        +------+
\ ```
\ 
\ M7: 關閉並記錄閥門與 deionized pump
\ 
\ ```
\  T1-M7 +------+
\ --+----| M7-0 |
\        +------+
\ ```
\ 
\ 
\ 
\ Step and Transition Table:
\ 
\ Step
\ 
\ | 狀態  | forth 指令            | 說明                                     |
\ |-------|-----------------------|------------------------------------------|
\ | M1-0  | drive-off-process     | 註1                                      |
\ | --    | --                    | --                                       |
\ | M4-0  | aqua-off              | 關閉 電解電源系統 與 電解液系統          |
\ | --    | --                    | --                                       |
\ | M5-3  | drive-off-x           | servo off x                              |
\ | M5-4  | x-off-delay           | time delay 200 ms                        |
\ | M5-5  | drive-off-y           | servo off y                              |
\ | M5-6  | y-off-delay           | time delay 200 ms                        |
\ | M5-7  | drive-off-z           | servo off z                              |
\ | M5-8  | z-off-delay           | time delay 200 ms                        |
\ | M5-9  | drive-off-v           | servo off v                              |
\ | M5-10 | v-off-delay           | time delay 200 ms                        |
\ | --    | --                    | --                                       |
\ | M6-0  | !io-off               | I/O 關閉                                 |
\ | --    | --                    | --                                       |
\ | M7-0  | !valve-off            | 紀錄並關閉閥門與 導電幫浦                |
\ | --    | --                    | --                                       |
\ | M8-0  | pw-supply-off         | 關閉 Power Supply                        |
\ 
\ 註: 
\ 1. drive-state = power-state = F, pw-led 滅。
\ 
\ 
\ | 狀態    | forth 指令           | 說明                 |
\ |---------|----------------------|----------------------|
\ | T1-1    | m1-02led-dim         | 註1                  |
\ | T1M5    | aqua-off2reset-fault | motion status = IDLE |
\ | T1M5-3  | off-x2dealy          | TRUE                 |
\ | T1M5-4  | delay2off-y          | TRUE                 |
\ | T1M5-5  | off-y2dealy          | TRUE                 |
\ | T1M5-6  | delay2off-z          | TRUE                 |
\ | T1M5-7  | off-z2dealy          | TRUE                 |
\ | T1M5-8  | delay2off-v          | TRUE                 |
\ | T1M5-9  | off-v2dealy          | TRUE                 |
\ | T1M5-10 | delay2io-off         | TRUE                 |
\ | T1-M6   | io-off2valve-off     | TRUE                 |
\ | T1-M7   | valve-off2pw-s-off   | TRUE                 |
\ 
\ 註1: power-off, EMS, system-off 按下 或 7 等級異警 或 CNC 程式要求自動關機
\ 
\ ------------------------------------------------------------------------------
\ 

\ 關閉 電解電源系統 與 電解液系統
: aqua-off
  \ 尚未規劃
;
\ servo off and power supply off
: x-drive-off-delay        ( Waiting ) ; \ time delay 200 ms
: y-drive-off-delay        ( Waiting ) ; \ time delay 200 ms
: z-drive-off-delay        ( Waiting ) ; \ time delay 200 ms
: v-drive-off-delay        ( Waiting ) ; \ time delay 200 ms

: drive-off-x
  drive-x drive-off
;
: drive-off-y
  drive-y drive-off
;
: drive-off-z
  drive-z drive-off
;
: drive-off-v
  drive-v drive-off
;


\ 關閉 power supply
: pw-supply-off
  -pw-supply-on
;

\ 關閉週邊
: io-off
\ turn off I/O
;
: valve-off
\ turn off valve
;


: shutdown
;

\ Transition activate

: drive-off2auqa         TRUE  ;

: aqua-off2reset-fault
  ECM-motion-state @ idle =
;

: off-x2dealy         TRUE  ;

: delay2off-y
  ['] x-drive-off-delay elapsed 500 >  if
      TRUE
    else
      FALSE
    then
;
: off-y2dealy         TRUE  ;

: delay2off-z
  ['] y-drive-off-delay elapsed 500 >  if
      TRUE
    else
      FALSE
    then
;
: off-z2dealy         TRUE  ;

: delay2off-v
  ['] z-drive-off-delay elapsed 500 >  if
      TRUE
    else
      FALSE
    then
;
: off-v2dealy         TRUE  ;

: delay2io-off
  ['] v-drive-off-delay elapsed 200 >  if
      TRUE
    else
      FALSE
    then
;
: io-off2valve-off     TRUE ;
: valve-off2pw-s-off    TRUE ;
: pt2-e
  TRUE
;

\ Step Definition
step aqua-off

step drive-off-x
step x-drive-off-delay
step drive-off-y
step y-drive-off-delay
step drive-off-z
step z-drive-off-delay
step drive-off-v
step v-drive-off-delay

step io-off
step valve-off
step pw-supply-off



\ Transition Definition


transition drive-off2auqa
transition aqua-off2reset-fault

transition off-x2dealy
transition delay2off-y
transition off-y2dealy
transition delay2off-z
transition off-z2dealy
transition delay2off-v
transition off-v2dealy
transition delay2io-off

transition io-off2valve-off
transition valve-off2pw-s-off
transition pt2-e



\ Link

' drive-off-process         ' drive-off2auqa      -->
' drive-off2auqa         ' aqua-off               -->
' aqua-off                  ' aqua-off2reset-fault   -->
' aqua-off2reset-fault      ' drive-off-x            -->
' drive-off-x               ' off-x2dealy            -->
' off-x2dealy               ' x-drive-off-delay      -->
' x-drive-off-delay         ' delay2off-y            -->

\ drive-off-x --> drive-off-delay-x  --> drive-off-y.....
' delay2off-y               ' drive-off-y            -->
' drive-off-y               ' off-y2dealy            -->
' off-y2dealy               ' y-drive-off-delay      -->
' y-drive-off-delay         ' delay2off-z            -->

' delay2off-z               ' drive-off-z            -->
' drive-off-z               ' off-z2dealy            -->
' off-z2dealy               ' z-drive-off-delay      -->
' z-drive-off-delay         ' delay2off-v            -->

' delay2off-v               ' drive-off-v            -->
' drive-off-v               ' off-v2dealy            -->
' off-v2dealy               ' v-drive-off-delay      -->
' v-drive-off-delay         ' delay2io-off     -->

' delay2io-off         ' io-off                 -->

' io-off                    ' io-off2valve-off       -->
' io-off2valve-off          ' valve-off              -->
' valve-off                 ' valve-off2pw-s-off      -->
' valve-off2pw-s-off         ' pw-supply-off          -->
' pw-supply-off             ' all-drive-off?         -->
' all-drive-off?            ' already-pw-off         -->



\ ------------------------------------------------------------------------------
\ drive on process
\ ------------------------------------------------------------------------------
\ 說明:
\ 
\ `reset-fault` 並使用 `CSP mode`, 延遲 500ms 後各軸 drive on (每軸 Drive on 時
\ 需間格200ms), drive on 後記錄 drive on 與 power on 狀態。
\ 
\ 
\ 
\ Graphics:
\ 
\ ```
\   T2-8  +------+ T3-2 +------+ T3-3 +------+ T3-4 +------+
\ ---+----| M3-0 |--+---| M3-2 |--+---| M3-3 |--+---| M3-4 |-----+
\         +------+      +------+      +------+      +------+     |
\                                                                |
\ +--------------------------------------------------------------+
\ |
\ | T3-5 +------+   T3-6 +------+ T3-7 +------+   T3-8 +------+ T3-9 +------+
\ +-+----| M3-5 |-+--+---| M3-6 |--+---| M3-7 |-+--+---| M3-8 |--+---| M3-9 |--+
\   |    +------+ |      +------+      +------+ |      +------+      +------+  |
\   | T3-5E       | T3-6E                       | T3-9E                        |
\   +--+---> M2   +--+---> M2                   +--+---> M2                    |
\                                                                              |
\ +----------------------------------------------------------------------------+
\ |
\ | T3-10 +-------+   T3-11 +-------+ T3-12 +-------+   T3-13 +-------+ T3-13
\ +--+----| M3-10 |-+--+----| M3-11 |--+----| M3-12 |-+--+----| M3-13 |--+-----+
\         +-------+ |       +-------+       +-------+ |       +-------+        |
\                   | T3-11E                          | T3-13E                 |
\                   +--+---> M2                       +--+---> M2              |
\                                                                              |
\ +----------------------------------------------------------------------------+
\ |
\ |     +-------+ T2-E
\ +-----| M3-14 |--+---> S2
\       +-------+
\ ```
\ 
\ Step and Transition Table:
\ 
\ Step
\ 
\ | 狀態  | forth 指令        | 說明                          |
\ |-------|-------------------|-------------------------------|
\ | M3-0  | drive-on-process  | init of m3, 開啟 Power Supply |
\ | M3-2  | p-on-supply-delay | Delay of Power Supply         |
\ | M3-3  | reset-all-drive   | reset falut and use csp mode  |
\ | M3-4  | reset-drive-delay | Time Delay                    |
\ | M3-5  | drive-on-x        | Drive on x                    |
\ | M3-6  | check-drive-x     | Time Delay                    |
\ | M3-7  | drive-on-y        | Drive on y                    |
\ | M3-8  | check-drive-y     | Time Delay                    |
\ | M3-9  | drive-on-z        | Drive on z                    |
\ | M3-10 | check-drive-z     | Time Delay                    |
\ | M3-11 | drive-on-v        | Drive on v                    |
\ | M3-12 | check-drive-v     | Time Delay                    |
\ | M3-13 | +states-toggle    | 切換 drive-state, power-state |
\ | M3-14 | already-pw-on     | 完成 power-on                 |
\ 
\ 
\ Transition
\ 
\ | 狀態   | forth 指令            | 說明                            |
\ |--------|-----------------------|---------------------------------|
\ | T3-2   | p-supply2delay        | TRUE                            |
\ | T3-3   | delay2reset-all-drive | Delay 500 ms                    |
\ | T3-4   | reset2delay           | TRUE                            |
\ | T3-5   | delay2on-x            | Delay 500 ms                    |
\ | T3-5E  | reset-drive-err       | reset drive 失敗                |
\ | T3-6   | on-x2delay            | TRUE                            |
\ | T3-6E  | x-drive-on-err        | X 軸 drive-on 失敗              |
\ | T3-7   | delay2on-y            | Delay 200 ms                    |
\ | T3-8   | on-y2delay            | TRUE                            |
\ | T3-8E  | y-drive-on-err        | Y 軸 drive-on 失敗              |
\ | T3-9   | delay2on-z            | Delay 200 ms                    |
\ | T3-10  | on-z2delay            | TRUE                            |
\ | T3-10E | z-drive-on-err        | Z 軸 drive-on 失敗              |
\ | T3-11  | delay2on-v            | Delay 200 ms                    |
\ | T3-12  | on-v2delay            | TRUE                            |
\ | T3-12E | v-drive-on-err        | V 軸 drive-on 失敗              |
\ | T3-13  | delay2+s-toggle       | Delay 200 ms                    |
\ | T2-E   | all-drive-on?         | 所有軸是否完成 drive-on         |
\ 
\ ------------------------------------------------------------------------------

variable $x-drive-err
variable $y-drive-err
variable $z-drive-err
variable $v-drive-err

\ Step Activity
: p-on-supply-delay     ( Waiting );
: reset-all-drive
\ Use csp mode
  csp drive-x op-mode!
  csp drive-y op-mode!
  csp drive-z op-mode!
  csp drive-v op-mode!

  drive-x reset-fault
  drive-y reset-fault
  drive-z reset-fault
  drive-v reset-fault
  +coordinator
;
: reset-drive-delay    ( Waiting ) ;
: drive-on-x
  drive-x drive-on
;
: check-drive-x
  ( Waiting )
;
: drive-on-y
  drive-y drive-on
;
: check-drive-y
  ( Waiting )
;
: drive-on-z
  drive-z drive-on
;
: check-drive-z
  ( Waiting )
;
: drive-on-v
  drive-v drive-on
;
: check-drive-v
  ( Waiting )
;
: +states-toggle
  TRUE $drive-state !
;

\ Transition Activity
: p-supply2delay           TRUE ;
: delay2reset-all-drive
  ['] p-on-supply-delay elapsed 500 >  if
    TRUE
  else
    FALSE
  then
;
: pt2m3-1                   TRUE ;
: reset2delay              TRUE ;
: delay2on-x
  ['] reset-drive-delay elapsed 500 >  if
    drive-x drive-fault? not
    drive-y drive-fault? not
    drive-z drive-fault? not
    drive-v drive-fault? not
    and and and
  else
    FALSE
  then

;
: reset-drive-err
  ['] reset-drive-delay elapsed 500 >  if
    delay2on-x not
  else
    FALSE
  then
;
: on-x2delay               TRUE ;
: delay2on-y
  ['] check-drive-x elapsed 200 >  if
    drive-x drive-on?
  else
    FALSE
  then
;
: x-drive-on-err \ x 軸 drive-on 失敗
  ['] check-drive-x elapsed 200 >  if
    drive-x drive-on? not if
      ." drive-on-err|x" cr
      TRUE
    else
      FALSE
    then
  else
    FALSE
  then
;

: on-y2delay               TRUE ;
: delay2on-z
  ['] check-drive-y elapsed 200 >  if
    drive-y drive-on?
  else
    FALSE
  then
;
: y-drive-on-err \ y 軸 drive-on 失敗
  ['] check-drive-y elapsed 200 >  if
    drive-y drive-on? not if
      ." drive-on-err|y" cr
      TRUE
    else
      FALSE
    then
  else
    FALSE
  then
;
: on-z2delay               TRUE ;
: delay2on-v
  ['] check-drive-z elapsed 200 >  if
    drive-z drive-on?
  else
    FALSE
  then
;
: z-drive-on-err \ z 軸 drive-on 失敗
  ['] check-drive-z elapsed 200 >  if
    drive-z drive-on? not if
      ." drive-on-err|z" cr
      TRUE
    else
      FALSE
    then
  else
    FALSE
  then
;
: on-v2delay               TRUE ;
: delay2+s-toggle
  ['] check-drive-v elapsed 200 >  if
    drive-v drive-on?
  else
    FALSE
  then
;
: v-drive-on-err \ z 軸 drive-on 失敗
  ['] check-drive-v elapsed 200 >  if
    drive-v drive-on? not if
      ." drive-on-err|v" cr
      TRUE
    else
      FALSE
    then
  else
    FALSE
  then
;




\ Step Definition
step p-on-supply-delay
step reset-all-drive
step reset-drive-delay
step drive-on-x
step check-drive-x
step drive-on-y
step check-drive-y
step drive-on-z
step check-drive-z
step drive-on-v
step check-drive-v
step +states-toggle

\ Transition Definition
transition pt2m3-1
transition p-supply2delay
transition delay2reset-all-drive
transition reset2delay
transition delay2on-x
transition reset-drive-err
transition on-x2delay
transition delay2on-y
transition x-drive-on-err
transition on-y2delay
transition delay2on-z
transition y-drive-on-err
transition on-z2delay
transition delay2on-v
transition z-drive-on-err
transition on-v2delay
transition delay2+s-toggle
transition v-drive-on-err

\ Link
' drive-on-process       ' p-supply2delay         -->
' p-supply2delay         ' p-on-supply-delay      -->
' p-on-supply-delay      ' delay2reset-all-drive  -->
' delay2reset-all-drive  ' reset-all-drive        -->
' reset-all-drive        ' reset2delay            -->
' reset2delay            ' reset-drive-delay      -->
\ reset drive 成功
' reset-drive-delay      ' delay2on-x             -->
' delay2on-x             ' drive-on-x             -->
\ reset drive 不成功
' reset-drive-delay      ' reset-drive-err        -->
' reset-drive-err        ' drive-off-process      -->
\ 進入 drive-on
' drive-on-x             ' on-x2delay             -->
' on-x2delay             ' check-drive-x          -->

\ axis-x drive-on faile
' check-drive-x          ' x-drive-on-err         -->
' x-drive-on-err         ' drive-off-process      -->
\ axis-x drive-on success
' check-drive-x          ' delay2on-y             -->
' delay2on-y             ' drive-on-y             -->
' drive-on-y             ' on-y2delay             -->
' on-y2delay             ' check-drive-y          -->

\ axis-y drive-on faile
' check-drive-y          ' y-drive-on-err         -->
' y-drive-on-err         ' drive-off-process      -->
\ axis-y drive-on success
' check-drive-y          ' delay2on-z             -->
' delay2on-z             ' drive-on-z             -->
' drive-on-z             ' on-z2delay             -->
' on-z2delay             ' check-drive-z          -->

\ axis-z drive-on faile
' check-drive-z          ' z-drive-on-err         -->
' z-drive-on-err         ' drive-off-process      -->
\ axis-z drive-on success
' check-drive-z          ' delay2on-v             -->
' delay2on-v             ' drive-on-v             -->
' drive-on-v             ' on-v2delay             -->
' on-v2delay             ' check-drive-v          -->

\ axis-v drive-on faile
' check-drive-v          ' v-drive-on-err         -->
' v-drive-on-err         ' drive-off-process      -->
\ axis-v drive-on success
' check-drive-v          ' delay2+s-toggle        -->
' delay2+s-toggle        ' +states-toggle         -->

\ 完成 power-on
' +states-toggle         ' all-drive-on?          -->
' all-drive-on?          ' already-pw-on          -->


marker -work


