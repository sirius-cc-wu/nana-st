-work

\ Actions
variable aqua-prcs-pump-b ( S: bit )
: +aqua-prcs-pump ( -- ) aqua-prcs-pump-b @ aqua-prcs-pump-aout ec-aout! ;

fvariable aqua-prcs-pump-p ( F: bar )
: aqua-prcs-pump-p! ( F: bar -- )
  \ 限制指令輸入值 0e~10e
  fdup 0e f< if
    fdrop 0e
  else
    fdup 10e fswap f< if
      fdrop 10e
    then
  then
  fdup aqua-prcs-pump-p f!
  \ 將氣壓  值轉為bit
  3276.7e f* f>s aqua-prcs-pump-b !
;
: aqua-prcs-pump-p@ ( F: -- bar ) aqua-prcs-pump-p f@ ;

\ aqua machine state
: aqua-machine-s? ( -- flag )
  is-EMS? not aqua-ems@ aqua-ready@ upper-close?
  platform-door@
  and and and and
;

create aqua-calibration falign 12 floats allot

\ gain-array
: aqua-calibration! ( index -- ) ( F: value -- )
    aqua-calibration fparam! ; 
: aqua-calibration@ ( index -- ) ( F: -- value )
    aqua-calibration fparam@ ;  

\ 因aqua設定水壓值與實際有出入,故藉由此陣列來校正
0e 0 aqua-calibration!   
0.7e 1 aqua-calibration!        \   目標為1時,須設定為0.7e  
1.3e 2 aqua-calibration!
1.95e  3 aqua-calibration!
2.6e  4 aqua-calibration!
3.2e  5 aqua-calibration!
3.85e 6 aqua-calibration!
4.5e 7 aqua-calibration!
5.1e 8 aqua-calibration!
9e  9 aqua-calibration!         \   流速上限受限於管徑,水壓實際上是無法到達的,目前不調整
10e 10 aqua-calibration!
10e  11 aqua-calibration! 


variable mod-aqua-value mod-aqua-value off
: trans-aqua-value      ( F: target-bar -- real-bar )
    mod-aqua-value @ if
        fdup 
        fdup floor f- 
        fswap f>s 
        dup 1+
        fdup
        aqua-calibration@ f* fswap
        aqua-calibration@ fswap fnegate 1e f+ f* f+
    then
;

\ 將轉換後的壓力值輸入到aqua上
: trans-aqua-prcs-pump-p! ( F: bar -- )
    trans-aqua-value aqua-prcs-pump-p!
;

fvariable test-aqua-value
fvariable cutting-aqua-value
fvariable current-aqua-value

\ 根據PECM-motion-state去決定要將加工的水壓值或測試用的水壓值設定到aqua
\ 其中 因設定的值與實際量測的壓力值有些出入, 故利用aqua-calibration陣列去做調整
\ 倘若不想經由陣列做調整, 將mod-aqua-value關閉即可
: set-aqua-value       ( -- )
  PECM-motion-state @ idle =
  machining-mode @ none = and if
    test-aqua-value f@
  else
    cutting-aqua-value f@
  then
  fdup current-aqua-value f!
  trans-aqua-prcs-pump-p! +aqua-prcs-pump
;


: aqua-machine-error
  is-EMS? if
    ." error| Emergency stop. ;A2101"
  then

  aqua-ems@ not if
    ." error| Aqua emergency stop. ;A2102"
  then

  aqua-ready@ not if
    ." error| Aqua not ready. ;A2103"
  then

  platform-door@ not if
    ." error| CNC security door not closed. ;A2104"
  then

  upper-close? not if
    ." error| CNC upper door not closed. ;A2104"
  then
;

variable aqua-user-axis 2 aqua-user-axis !
: +aqua-user-axis
    aqua-user-axis @   
    case
      1 of  xa-water-dout +dout  endof
      2 of  xb-water-dout +dout  endof
      3 of  xc-water-dout +dout  endof
    endcase
;
: -aqua-user-axis
      xa-water-dout -dout
      xb-water-dout -dout
      xc-water-dout -dout
;

variable $aqua-run   FALSE $aqua-run !
\ Aqua Machine start/stop command
: +aqua-run
  TRUE $aqua-run !
;
: -aqua-run
  FALSE $aqua-run !
;

\ Aqua System SFC
\ 
\ 說明:
\ 正當程序：
\ 當下出 +aqua-run, 則開始此sfc流程, 則依序設定的水壓行事
\ 當下出 -aqua-run, 因保護振動軸與管路, 不可直接關閉, 此sfc會慢慢將水壓下降到0, 才進行關閉

\ 錯誤程序：
\ 當下出 +aqua-run 則開始此sfc流程, 若開啟失敗, 例如cnc加工門未關, 緊急開關等狀況, 
\ 一律馬上將水壓設定為0, 並馬上關閉所有訊號
\ 
\ sfc 流程圖
\ 
\               aqua-m-init
\                   |
\                   +   aqua-m-run?
\                   |
\                   v
\               aqua-m-wait1
\                   |
\                   +-----------------------+
\                   |                       |
\                   + aqua-m-ready?         + aqua-m-not-ready?
\                   |                       |
\                   v                       v
\               aqua-m-success          aqua-m-fail
\                   |                       |
\                   + aqua-m-t1?            + aqua-m-t2?
\                   |                       |
\                   v                       v
\               aqua-m-wait2            aqua-m-init
\                   |
\                   +-----------------------+
\                   |                       |
\                   + aqua-m-not-run?       + aqua-m-run-fail?
\                   |                       |
\                   v                       v
\               aqua-m-drop-pre         aqua-m-fail
\                   |
\                   +   aqua-pre=0?
\                   |
\                   v
\               aqua-m-wait3
\                   |
\                   +   aqua-m-delay-1?
\                   |
\                   v
\               aqua-m-closing
\                   |
\                   +   aqua-m-t3?
\                   |
\                   v
\               aqua-m-init

\ aqua machine
\ step
variable aqua-drop-pre-time
variable aqua-state

\ aqua初始狀態, 並等待+aqua-run開啟aqua
: aqua-m-init
  ( do nothing )
;
step aqua-m-init

\ 等待一個週期, 用於判斷目前狀態能否進行出水
: aqua-m-wait1
  ( do nothing )
;
step aqua-m-wait1

\ 倘若aqua 開啟失敗, 此時報錯與關閉給aqua的所有訊號, 並回到aqua-m-init
: aqua-m-fail
  \ 關閉製程pump運轉
  -aqua-prcs-pump-start
  \ 關閉pump閥
  -aqua-prcs-valve
  \ 將水壓設定為0
  0e aqua-prcs-pump-p!
  \ 關閉電解液系統
  -aqua-rls-elect
  \ 報錯
  aqua-machine-error
  -aqua-run
  -aqua-user-axis
  aqua-state off
  0 15 RMT-led!
;
step aqua-m-fail

\ 倘若aqua 成功開啟, 則將所需io設定開啟
: aqua-m-success
  \ 將壓力值設定到 I/O 上
  set-aqua-value
  \ 開啟製程pump運轉
  +aqua-prcs-pump-start
  \ 開啟pump閥
  +aqua-prcs-valve
  \ 開啟電解液系統
  +aqua-rls-elect
  \ 開啟選擇的出水口
  PECM-user-axis @ aqua-user-axis !
  +aqua-user-axis
  1 15 RMT-led!
  500 aqua-drop-pre-time !
  aqua-state on
;
step aqua-m-success

\ 等待正常關閉或緊急錯誤
\ 其中緊急錯誤如緊急停止, 開啟加工門等
: aqua-m-wait2
  ( do nothing )
;
step aqua-m-wait2

\ 在關閉水壓時, 需要先將水壓慢慢關閉, 不可直接切掉訊號, 故這邊以-1.5bar/T 的速度下降, 直到為0
\ 其中T 為下降週期, 目前設定500ms

: aqua-m-drop-pre
  aqua-drop-pre-time @ 500 >= if
    current-aqua-value f@ 1.5e f- 0e fmax
    fdup current-aqua-value f! trans-aqua-prcs-pump-p!
    +aqua-prcs-pump
    0 aqua-drop-pre-time !
  else
    1 aqua-drop-pre-time +!
  then 
;
step aqua-m-drop-pre

: aqua-m-wait3
  ( do nothing )
;
step aqua-m-wait3

\ 當水壓已固定斜率下降到0時, 才把其餘訊號關閉
: aqua-m-closing
  \ 關閉製程pump運轉
  -aqua-prcs-pump-start
  \ 關閉pump閥
  -aqua-prcs-valve
  \ 關閉水系統
  -aqua-rls-elect
  aqua-state off
  -aqua-user-axis
  0 15 RMT-led!
;
step aqua-m-closing

\ transition
: aqua-m-run?
  $aqua-run @
;
transition aqua-m-run?

: aqua-m-ready?
  aqua-machine-s?
;
transition aqua-m-ready?

: aqua-m-not-ready?
  aqua-machine-s? not
;
transition aqua-m-not-ready?

: aqua-m-t1?
  true
;
transition aqua-m-t1?

: aqua-m-t2?
  true
;
transition aqua-m-t2?

: aqua-m-not-run?
  aqua-m-run? not
;
transition aqua-m-not-run?

: aqua-m-run-fail?
  aqua-m-run? 
  aqua-m-not-ready?
  and
;
transition aqua-m-run-fail?

: aqua-pre=0?
  current-aqua-value f@ 0.1e f<
;
transition aqua-pre=0?

: aqua-m-delay-1?
  ['] aqua-m-wait3 elapsed 5000 >
;
transition aqua-m-delay-1?

: aqua-m-t3?
  true
;
transition aqua-m-t3?

\ Link
' aqua-m-init         ' aqua-m-run?               -->
' aqua-m-run?         ' aqua-m-wait1              -->
 
' aqua-m-wait1        ' aqua-m-ready?             -->
' aqua-m-wait1        ' aqua-m-not-ready?         -->
 
' aqua-m-not-ready?   ' aqua-m-fail               -->
' aqua-m-fail         ' aqua-m-t2?                -->
' aqua-m-t2?          ' aqua-m-init               -->

' aqua-m-ready?       ' aqua-m-success            -->
' aqua-m-success      ' aqua-m-t1?                -->
' aqua-m-t1?          ' aqua-m-wait2              -->
 
' aqua-m-wait2        ' aqua-m-run-fail?          -->
' aqua-m-run-fail?    ' aqua-m-fail               -->

' aqua-m-wait2        ' aqua-m-not-run?           -->
' aqua-m-not-run?     ' aqua-m-drop-pre           -->
' aqua-m-drop-pre     ' aqua-pre=0?               -->
' aqua-pre=0?         ' aqua-m-wait3              -->
' aqua-m-wait3        ' aqua-m-delay-1?           -->
' aqua-m-delay-1?     ' aqua-m-closing            -->
' aqua-m-closing      ' aqua-m-t3?                -->
' aqua-m-t3?          ' aqua-m-init               -->

\ 印出aqua狀態
: .aqua-fault ( -- )
    aqua-ems@ aqua-ready@ and
    if
        ." aqua_fault|false" cr
    else
        ." aqua_fault|true" cr
    then
;


\ 印出aqua壓力
\ 0-32767對應 0-26.5 bar
: .aqua-press ( -- )
    ." aqua_press|" m1-press-ain ec-ain@ s>f 0.0008089e f* 4 2 f.r cr
;

\ 印出aqua流量
\ 0-32767對應0-1500 l/min
: .aqua-flow ( -- )
    ." aqua_flow|" m1-flow-ain ec-ain@ s>f 0.04578e f* 4 2 f.r cr
;

marker -work