-work

\ Actions
variable aqua-prcs-pump-b ( S: bit )
: +aqua-prcs-pump ( -- ) aqua-prcs-pump-b @ aqua-prcs-pump-aout ec-aout! ;

fvariable aqua-prcs-pump-p ( F: bar )
: aqua-prcs-pump-p! ( F: bar -- )
  \ 限制指令輸入值 0e~16e
  fdup 0e f< if
    fdrop 0e
  else
    fdup 16e fswap f< if
      fdrop 16e
    then
  then
  fdup aqua-prcs-pump-p f!
  \ 將氣壓  值轉為bit
  2047.9375e f* f>s aqua-prcs-pump-b !
;
: aqua-prcs-pump-p@ ( F: -- bar ) aqua-prcs-pump-p f@ ;
: .aqua-prcs-pump-p ( -- ) ." |aqua-prcs-pump-p|" aqua-prcs-pump-p f@ f. ;
: .aqua-prcs-pump-b ( -- ) ." |aqua-prcs-pump-b|" aqua-prcs-pump-b @ . ;


: .aqua-fb-p ( -- ) ." |aqua-fb-p|" aqua-fb-p@ f. ;

\ 壓力差的門檻值, 單位: bar。
fvariable aqua-tol-p   1e aqua-tol-p f!
: aqua-tol-p! ( F: bar -- ) aqua-tol-p f! ;
: aqua-tol-p@ ( F: -- bar ) aqua-tol-p f@ ;
: .aqua-tol-p ( -- ) ." |aqua-tol-p|" aqua-tol-p f@ f. ;
: aqua-p-diff? ( -- flag )
  aqua-prcs-pump-p@ aqua-fb-p@ f-
  fabs aqua-tol-p@ f< not
;

\ aqua machine state
: aqua-machine-s? ( -- flag )
  is-EMS? not aqua-ems@ aqua-ready@
  platform-door@
  and and and
;

create aqua-calibration falign 18 floats allot


\ gain-array
: aqua-calibration! ( index -- ) ( F: value -- )
    aqua-calibration fparam! ; 
: aqua-calibration@ ( index -- ) ( F: -- value )
    aqua-calibration fparam@ ;  

\ 因aqua設定水壓值與實際有出入,故藉由此陣列來校正
0e   0 aqua-calibration!   
1e   1 aqua-calibration!        \   目標為1時,須設定為0.7e  
2e   2 aqua-calibration!
3e   3 aqua-calibration!
4e   4 aqua-calibration!
5e   5 aqua-calibration!
6e   6 aqua-calibration!
7e   7 aqua-calibration!
8e   8 aqua-calibration!
9e   9 aqua-calibration!         \   流速上限受限於管徑,水壓實際上是無法到達的,目前不調整
10e  10 aqua-calibration!
11e  11 aqua-calibration!
11e  12 aqua-calibration!
12e  13 aqua-calibration!
14e  14 aqua-calibration!
15e  15 aqua-calibration!
16e  16 aqua-calibration!
16e  17 aqua-calibration!

variable mod-aqua-value mod-aqua-value on
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

\ 根據ECM-motion-state去決定要將加工的水壓值或測試用的水壓值設定到aqua
\ 其中 因設定的值與實際量測的壓力值有些出入, 故利用aqua-calibration陣列去做調整
\ 倘若不想經由陣列做調整, 將mod-aqua-value關閉即可
: set-aqua-value       ( -- )
  ECM-motion-state @ idle =
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
;

variable $aqua-run   FALSE $aqua-run !
variable $buft-run   FALSE $buft-run !
variable $feedback-run   FALSE $feedback-run !

\ Aqua Machine start/stop command
: +aqua-run
  TRUE $aqua-run !
;
: -aqua-run
  FALSE $aqua-run !
;
\ Bufer Tank start/stop command
: +buft-run
  TRUE $buft-run !
;
: -buft-run
  FALSE $buft-run !
;
\ Feedback start/stop command
: +fb-run
  TRUE $feedback-run !
;
: -fb-run
  FALSE $feedback-run !
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
  aqua-state off
  \ 關閉v-air-pump
  close-air-pump

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
  \ 關閉v-air-pump
  close-air-pump
  aqua-state off
  
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
' buft-init    ' high-level        -->
' buft-init    ' middle-level      -->
' buft-init    ' low-level         -->

' high-level    ' buft-pump-start  -->
' middle-level    ' buft-init      -->
' low-level    ' buft-pump-stop    -->
' buft-pump-start    ' aqua-t19    -->
' buft-pump-stop    ' aqua-t20     -->

' aqua-t19    ' buft-init          -->
' aqua-t20    ' buft-init          -->

\ Feedback (Pressure)
\ 
\ 說明:
\ 
\ 正常流程:
\ 
\ a1: 若壓力差 > 壓力差的門檻值, 則流程回到 aqua-m-init。
\ a2: 若壓力差 < 壓力差的門檻值, 繼續進行目前的任務。
\ 
\ 異常流程:
\ 
\ 
\ SFC 流程圖:
\ 
\ +----+     FT1
\ | F0 +---+--+---> F0
\ +----+   |
\          | FT2 +----+ TURE
\          +--+--+ F1 +--+---> F0
\                +----+
\ 
\ 
\ Step table
\ 
\ | 狀態 | forth 指令    | 說明                     |
\ |------|---------------|--------------------------|
\ | F0   | feedback-init | Feedbakc init            |
\ | F1   | feedback-s1   | 確認 aqua machine 的狀態 |
\ 
\ Transition table
\ 
\ | 狀態 | forth 指令          | 說明                      |
\ |------|---------------------|---------------------------|
\ | FT1  | p-diff-not-over-tol | 壓力值差 `<` 設定的公差值 |
\ | FT2  | p-diff-over-tol     | 壓力值差 `>` 設定的公差值 |




\ Step
: feedback-init
  \ do nothing
;
: feedback-s1
  aqua-m-init
;

\ Transition
: p-diff-over-tol
  \ Pressure difference > tolerance
  $feedback-run @ if
    ['] feedback-init elapsed 3000 >  if
      aqua-p-diff?
    else
      FALSE
    then
  else
    FALSE
  then
;
: p-diff-not-over-tol
  \ Pressure difference < tolerance
  $feedback-run @ if
    ['] feedback-s1 elapsed 3000 >  if
      \ p-diff-over-tol not
      aqua-p-diff? not
    else
      FALSE
    then
  else
    FALSE
  then
;
: feedback-t1   TRUE ;


\ Step Definition
step feedback-init
step feedback-s1

\ Transition Definition
transition p-diff-over-tol
transition p-diff-not-over-tol
transition feedback-t1

\ Link
' feedback-init        ' p-diff-over-tol      -->
' feedback-init        ' p-diff-not-over-tol  -->
' p-diff-over-tol      ' feedback-s1          -->
' p-diff-not-over-tol  ' feedback-init        -->
' feedback-s1          ' feedback-t1          -->
' feedback-t1          ' feedback-init        -->


\ start aqua process
: aqua-init
  \ Aqua machine
  ['] aqua-m-init +step
  \ Buffer tank
  ['] buft-init +step
  \ Feedback
  ['] feedback-init +step
;

\ 印出aqua狀態
: .aqua-fault ( -- )
    aqua-ems@ aqua-ready@ and
    if
        ." aqua_fault|false" cr
    else
        ." aqua_fault|true" cr
    then
;

\ 印出aqua流量
: .aqua-flow ( -- )
    ." aqua_flow|" aqua-flow ec-ain@ s>f 0.003e f* 0.8e f- 0e fmax 4 1 f.r cr
;

\ 印出aqua溫度
: .aqua-temp ( -- )
    ." aqua_temp|" aqua-temp ec-ain@ s>f 0.0023e f* 1.1397e f+ 4 1 f.r cr
;

marker -work
