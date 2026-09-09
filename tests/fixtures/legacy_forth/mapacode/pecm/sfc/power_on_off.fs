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
: btn-toggle?
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

: sys-off-btn?
  $sys-btn3 @ $sys-btn4 !
  $sys-btn2 @ $sys-btn3 !
  $sys-btn1 @ $sys-btn2 !
  sys-off-btn@ 0<> $sys-btn1 !
  $sys-btn1 @ $sys-btn2 @ $sys-btn3 @ not $sys-btn4 @ not and and and $sys-off-state !
;

\ 確認三相電源
: 3phase-err? ( -- flag )
  3phase-din ec-din@ not
;
: .pw-on-error-msg
  \ 三相電源異常
  3phase-err? if
    ." error|Three phase detector error.;A1102" cr
  then
  is-EMS? if
    ." error|In EMS.;A1103" cr
  then

;
: check-drive-on-peripherals
  
  \ 檢查三相電源
  3phase-err?  
  \ 檢查緊急開關
  is-EMS?   
  or $peripherals-err !
  $peripherals-err @ if
    .pw-on-error-msg
  then
;

: check-pw-on-peripherals
  check-drive-on-peripherals
  \ 檢查 following-error 過大
  drive-xa drive-fault? if
    ." error|XA drive-fault" cr
    \ 週邊異常旗標
    TRUE $peripherals-err !
  then
  drive-xb drive-fault? if
    ." error|XB drive-fault" cr
    \ 週邊異常旗標
    TRUE $peripherals-err !
  then
  drive-xc drive-fault? if
    ." error|XC drive-fault" cr
    \ 週邊異常旗標
    TRUE $peripherals-err !
  then
  drive-w drive-fault? if
    ." error|W drive-fault" cr
    \ 週邊異常旗標
    TRUE $peripherals-err !
  then
  
  drive-dl drive-fault? if
    ." error|DL drive-fault" cr
    \ 週邊異常旗標
    TRUE $peripherals-err !
  then
  
  drive-dr drive-fault? if
    ." error|DR drive-fault" cr
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
  TRUE PECM-power-state !
  +pw-led
  check-pw-on-peripherals
  \ 給dsp使用
  1 mdu-on!
;

: pw-on-process
  check-drive-on-peripherals
;

: drive-on-process
 \ 驅動器送電
  +pw-supply-on

  \ munk有連接時 將munk power on
  munk-configured @ if
    +munk-pw-on
  then
;

: drive-off-process
  \ FALSE $peripherals-err !
  FALSE PECM-power-state !
  FALSE $drive-state !
  -pw-led
  0 mdu-on!
;

\ Transition Activity
: check-pw-btn
  btn-toggle? 
  $pw-btn-state @
  system-ready? and
;

: into-drive-off?
  btn-toggle?
  sys-off-btn?
  
  $peripherals-err @
  $pw-btn-state @
  $sys-off-state @ or or 
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
\ | M5-3  | drive-off-xa           | servo off xa                              |
\ | M5-4  | x-off-delay           | time delay 200 ms                        |
\ | M5-5  | drive-off-xb           | servo off y                              |
\ | M5-6  | y-off-delay           | time delay 200 ms                        |
\ | M5-7  | drive-off-xc           | servo off z                              |
\ | M5-8  | z-off-delay           | time delay 200 ms                        |
\ | M5-9  | drive-off-w           | servo off v                              |
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
\ | T1M5-3  | off-xa2dealy          | TRUE                 |
\ | T1M5-4  | delay2off-xb          | TRUE                 |
\ | T1M5-5  | off-xb2dealy          | TRUE                 |
\ | T1M5-6  | delay2off-xc          | TRUE                 |
\ | T1M5-7  | off-xc2dealy          | TRUE                 |
\ | T1M5-8  | delay2off-w          | TRUE                 |
\ | T1M5-9  | off-w2dealy          | TRUE                 |
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
: xa-drive-off-delay        ( Waiting ) ; \ time delay 200 ms
: xb-drive-off-delay        ( Waiting ) ; \ time delay 200 ms
: xc-drive-off-delay        ( Waiting ) ; \ time delay 200 ms
: w-drive-off-delay         ( Waiting ) ; \ time delay 200 ms
: dl-drive-off-delay        ( Waiting ) ; \ time delay 200 ms
: dr-drive-off-delay        ( Waiting ) ; \ time delay 200 ms

: drive-off-xa
  drive-xa drive-off
  ." xa close"
;
: drive-off-xb
  drive-xb drive-off
  ." xb close"
;
: drive-off-xc
  drive-xc drive-off
  ." xc close"
;
: drive-off-w
  drive-w drive-off
  ." w close"
;
: drive-off-dl
  drive-dl drive-off
  ." dl close"
;
: drive-off-dr
  drive-dr drive-off
  ." dr close"
;


\ 關閉 power supply
: pw-supply-off
  -pw-supply-on
  
  \ munk有連接時 將munk power off
  munk-configured @ if
    +munk-pw-off
  then
;

\ 關閉週邊
: io-off
\ turn off I/O
;
: valve-off
\ turn off valve
;


\ Transition activate

: drive-off2auqa         TRUE  ;

: aqua-off2reset-fault
  PECM-motion-state @ idle =
;

: off-xa2dealy         TRUE  ;

: delay2off-xb
  ['] xa-drive-off-delay elapsed 500 >
;
: off-xb2dealy         TRUE  ;

: delay2off-xc
  ['] xb-drive-off-delay elapsed 500 >
;
: off-xc2dealy         TRUE  ;

: delay2off-w
  ['] xc-drive-off-delay elapsed 500 >
;
: off-w2dealy         TRUE  ;

: delay2off-dl
  ['] w-drive-off-delay elapsed 500 >
;
: off-dl2dealy         TRUE  ;

: delay2off-dr
  ['] dl-drive-off-delay elapsed 500 >
;
: off-dr2dealy         TRUE  ;

: delay2io-off
  ['] dr-drive-off-delay elapsed 200 >
;
: io-off2valve-off     TRUE ;
: valve-off2pw-s-off    TRUE ;

\ Step Definition
step aqua-off

step drive-off-xa
step xa-drive-off-delay
step drive-off-xb
step xb-drive-off-delay
step drive-off-xc
step xc-drive-off-delay
step drive-off-w
step w-drive-off-delay
step drive-off-dl
step dl-drive-off-delay
step drive-off-dr
step dr-drive-off-delay

step io-off
step valve-off
step pw-supply-off



\ Transition Definition


transition drive-off2auqa
transition aqua-off2reset-fault

transition off-xa2dealy
transition delay2off-xb
transition off-xb2dealy
transition delay2off-xc
transition off-xc2dealy
transition delay2off-w
transition off-w2dealy
transition delay2off-dl
transition off-dl2dealy
transition delay2off-dr
transition off-dr2dealy
transition delay2io-off

transition io-off2valve-off
transition valve-off2pw-s-off



\ Link

' drive-off-process         ' drive-off2auqa      -->
' drive-off2auqa         ' aqua-off               -->
' aqua-off                  ' aqua-off2reset-fault   -->
' aqua-off2reset-fault      ' drive-off-xa            -->
' drive-off-xa               ' off-xa2dealy            -->
' off-xa2dealy               ' xa-drive-off-delay      -->
' xa-drive-off-delay         ' delay2off-xb            -->

\ drive-off-xa --> drive-off-delay-xa  --> drive-off-xb.....
' delay2off-xb               ' drive-off-xb            -->
' drive-off-xb               ' off-xb2dealy            -->
' off-xb2dealy               ' xb-drive-off-delay      -->
' xb-drive-off-delay         ' delay2off-xc            -->

' delay2off-xc               ' drive-off-xc            -->
' drive-off-xc               ' off-xc2dealy            -->
' off-xc2dealy               ' xc-drive-off-delay      -->
' xc-drive-off-delay         ' delay2off-w            -->

' delay2off-w               ' drive-off-w            -->
' drive-off-w               ' off-w2dealy            -->
' off-w2dealy               ' w-drive-off-delay      -->
' w-drive-off-delay         ' delay2off-dl     -->

' delay2off-dl               ' drive-off-dl            -->
' drive-off-dl               ' off-dl2dealy            -->
' off-dl2dealy               ' dl-drive-off-delay      -->
' dl-drive-off-delay         ' delay2off-dr     -->

' delay2off-dr               ' drive-off-dr            -->
' drive-off-dr               ' off-dr2dealy            -->
' off-dr2dealy               ' dr-drive-off-delay      -->
' dr-drive-off-delay         ' delay2io-off     -->

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
\ | M3-5  | drive-on-xa        | Drive on x                    |
\ | M3-6  | check-drive-xa     | Time Delay                    |
\ | M3-7  | drive-on-xb        | Drive on y                    |
\ | M3-8  | check-drive-xb     | Time Delay                    |
\ | M3-9  | drive-on-xc        | Drive on z                    |
\ | M3-10 | check-drive-xc     | Time Delay                    |
\ | M3-11 | drive-on-w        | Drive on v                    |
\ | M3-12 | check-drive-w     | Time Delay                    |
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
\ | T3-5   | delay2on-xa            | Delay 500 ms                    |
\ | T3-5E  | reset-drive-err       | reset drive 失敗                |
\ | T3-6   | on-xa2delay            | TRUE                            |
\ | T3-6E  | xa-drive-on-err        | X 軸 drive-on 失敗              |
\ | T3-7   | delay2on-xb            | Delay 200 ms                    |
\ | T3-8   | on-xb2delay            | TRUE                            |
\ | T3-8E  | xb-drive-on-err        | Y 軸 drive-on 失敗              |
\ | T3-9   | delay2on-xc            | Delay 200 ms                    |
\ | T3-10  | on-xc2delay            | TRUE                            |
\ | T3-10E | xc-drive-on-err        | Z 軸 drive-on 失敗              |
\ | T3-11  | delay2on-w            | Delay 200 ms                    |
\ | T3-12  | on-w2delay            | TRUE                            |
\ | T3-12E | w-drive-on-err        | V 軸 drive-on 失敗              |
\ | T3-13  | delay2+s-toggle       | Delay 200 ms                    |
\ | T2-E   | all-drive-on?         | 所有軸是否完成 drive-on         |
\ 
\ ------------------------------------------------------------------------------


\ Step Activity
: p-on-supply-delay     ( Waiting );
: reset-all-drive
\ Use csp mode
  csp drive-xa op-mode!
  csp drive-xb op-mode!
  csp drive-xc op-mode!
  csp drive-w op-mode!
  csp drive-dl op-mode!
  csp drive-dr op-mode!

  drive-xa reset-fault
  drive-xb reset-fault
  drive-xc reset-fault
  drive-w reset-fault
  drive-dl reset-fault
  drive-dr reset-fault
  +coordinator
;
: reset-drive-delay    ( Waiting ) ;
: drive-on-xa
  drive-xa drive-on
  ." xa open"
;
: check-drive-xa
  ( Waiting )
;
: drive-on-xb
  drive-xb drive-on
  ." xb open"
;
: check-drive-xb
  ( Waiting )
;
: drive-on-xc
  drive-xc drive-on
  ." xc open"
;
: check-drive-xc
  ( Waiting )
;
: drive-on-w
  drive-w drive-on
  ." w open"
;
: check-drive-w
  ( Waiting )
;
: drive-on-dl
  drive-dl drive-on
  ." dl open"
;
: check-drive-dl
  ( Waiting )
;
: drive-on-dr
  drive-dr drive-on
  ." dr open"
;
: check-drive-dr
  ( Waiting )
;
: +states-toggle
  TRUE $drive-state !
;

\ Transition Activity
: p-supply2delay           TRUE ;
: delay2reset-all-drive
  ['] p-on-supply-delay elapsed 500 >
;
: pt2m3-1                   TRUE ;
: reset2delay              TRUE ;
: delay2on-xa
  ['] reset-drive-delay elapsed 500 >  if
    drive-xa drive-fault? not
    drive-xb drive-fault? not and
    drive-xc drive-fault? not and
    drive-w drive-fault? not and
    drive-dl drive-fault? not and
    drive-dr drive-fault? not and
  else
    FALSE
  then

;
: reset-drive-err
  ['] reset-drive-delay elapsed 500 >  if
    delay2on-xa not
  else
    FALSE
  then
;
: on-xa2delay               TRUE ;
: delay2on-xb
  ['] check-drive-xa elapsed 200 >  if
    drive-xa drive-on?  
  else
    FALSE
  then
;
: xa-drive-on-err \ x 軸 drive-on 失敗
  ['] check-drive-xa elapsed 200 >  if
    drive-xa drive-on? not if
      ." error|XA drive-on error" cr
      TRUE
    else
      FALSE
    then
  else
    FALSE
  then
;

: on-xb2delay               TRUE ;
: delay2on-xc
  ['] check-drive-xb elapsed 200 >  if
    drive-xb drive-on?
  else
    FALSE
  then
;
: xb-drive-on-err \ y 軸 drive-on 失敗
  ['] check-drive-xb elapsed 200 >  if
    drive-xb drive-on? not if
      ." error|XB drive-on error" cr
      TRUE
    else
      FALSE
    then
  else
    FALSE
  then
;
: on-xc2delay               TRUE ;
: delay2on-w
  ['] check-drive-xc elapsed 200 >  if
    drive-xc drive-on?
  else
    FALSE
  then
;
: xc-drive-on-err \ z 軸 drive-on 失敗
  ['] check-drive-xc elapsed 200 >  if
    drive-xc drive-on? not if
      ." error|XC drive-on error" cr
      TRUE
    else
      FALSE
    then
  else
    FALSE
  then
;
: on-w2delay               TRUE ;
: delay2+s-toggle
  ['] check-drive-dr elapsed 200 >  if
    drive-dr drive-on?
  else
    FALSE
  then
;
: w-drive-on-err \ w 軸 drive-on 失敗
  ['] check-drive-w elapsed 200 >  if
    drive-w drive-on? not if
      ." error|W drive-on error" cr
      TRUE
    else
      FALSE
    then
  else
    FALSE
  then
;

: on-dl2delay               TRUE ;
: delay2on-dr
  ['] check-drive-w elapsed 200 >  if
    drive-w drive-on?
  else
    FALSE
  then
;
: dl-drive-on-err \ dl 軸 drive-on 失敗
  ['] check-drive-dl elapsed 200 >  if
    drive-dl drive-on? not if
      ." drive-on-err|dl" cr
      TRUE
    else
      FALSE
    then
  else
    FALSE
  then
;

: on-dr2delay               TRUE ;
: delay2on-dl
  ['] check-drive-w elapsed 200 >  if
    drive-w drive-on?
  else
    FALSE
  then
;
: dr-drive-on-err \ dr 軸 drive-on 失敗
  ['] check-drive-dr elapsed 200 >  if
    drive-dr drive-on? not if
      ." drive-on-err|dr" cr
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
step drive-on-xa
step check-drive-xa
step drive-on-xb
step check-drive-xb
step drive-on-xc
step check-drive-xc
step drive-on-w
step check-drive-w
step drive-on-dl
step check-drive-dl
step drive-on-dr
step check-drive-dr
step +states-toggle

\ Transition Definition
transition pt2m3-1
transition p-supply2delay
transition delay2reset-all-drive
transition reset2delay
transition delay2on-xa
transition reset-drive-err
transition on-xa2delay
transition delay2on-xb
transition xa-drive-on-err
transition on-xb2delay
transition delay2on-xc
transition xb-drive-on-err
transition on-xc2delay
transition delay2on-w
transition xc-drive-on-err
transition on-w2delay
transition w-drive-on-err
transition on-dl2delay
transition dl-drive-on-err
transition delay2on-dl
transition on-dr2delay
transition delay2on-dr
transition dr-drive-on-err
transition delay2+s-toggle

\ Link
' drive-on-process       ' p-supply2delay         -->
' p-supply2delay         ' p-on-supply-delay      -->
' p-on-supply-delay      ' delay2reset-all-drive  -->
' delay2reset-all-drive  ' reset-all-drive        -->
' reset-all-drive        ' reset2delay            -->
' reset2delay            ' reset-drive-delay      -->
\ reset drive 成功
' reset-drive-delay      ' delay2on-xa             -->
' delay2on-xa             ' drive-on-xa             -->
\ reset drive 不成功
' reset-drive-delay      ' reset-drive-err        -->
' reset-drive-err        ' drive-off-process      -->
\ 進入 drive-on
' drive-on-xa             ' on-xa2delay             -->
' on-xa2delay             ' check-drive-xa          -->

\ axis-x drive-on faile
' check-drive-xa          ' xa-drive-on-err         -->
' xa-drive-on-err         ' drive-off-process      -->
\ axis-x drive-on success
' check-drive-xa          ' delay2on-xb             -->
' delay2on-xb             ' drive-on-xb             -->
' drive-on-xb             ' on-xb2delay             -->
' on-xb2delay             ' check-drive-xb          -->

\ axis-y drive-on faile
' check-drive-xb          ' xb-drive-on-err         -->
' xb-drive-on-err         ' drive-off-process      -->
\ axis-y drive-on success
' check-drive-xb          ' delay2on-xc             -->
' delay2on-xc             ' drive-on-xc             -->
' drive-on-xc             ' on-xc2delay             -->
' on-xc2delay             ' check-drive-xc          -->

\ axis-xc drive-on faile
' check-drive-xc          ' xc-drive-on-err         -->
' xc-drive-on-err         ' drive-off-process      -->
\ axis-xc drive-on success
' check-drive-xc          ' delay2on-w             -->
' delay2on-w             ' drive-on-w             -->
' drive-on-w             ' on-w2delay             -->
' on-w2delay             ' check-drive-w          -->

\ axis-w drive-on faile
' check-drive-w          ' w-drive-on-err         -->
' w-drive-on-err         ' drive-off-process      -->
\ axis-w drive-on success
' check-drive-w          ' delay2on-dl             -->
' delay2on-dl             ' drive-on-dl             -->
' drive-on-dl             ' on-dl2delay             -->
' on-dl2delay             ' check-drive-dl          -->

\ axis-dl drive-on faile
' check-drive-dl          ' dl-drive-on-err         -->
' dl-drive-on-err         ' drive-off-process      -->
\ axis-dl drive-on success
' check-drive-dl          ' delay2on-dr             -->
' delay2on-dr             ' drive-on-dr             -->
' drive-on-dr             ' on-dr2delay             -->
' on-dr2delay             ' check-drive-dr          -->

\ axis-dr drive-on faile
' check-drive-dr          ' dr-drive-on-err         -->
' dr-drive-on-err         ' drive-off-process      -->
\ axis-dr drive-on success
' check-drive-dr          ' delay2+s-toggle        -->
' delay2+s-toggle        ' +states-toggle         -->

\ 完成 power-on
' +states-toggle         ' all-drive-on?          -->
' all-drive-on?          ' already-pw-on          -->


marker -work
