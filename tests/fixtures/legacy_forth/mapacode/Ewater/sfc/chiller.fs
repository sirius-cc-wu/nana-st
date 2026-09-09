-work

\
: +chiller
    shimizu-valve-dout +dout
    m11-pump-dout +dout
;
: -chiller
    shimizu-valve-dout -dout
    m11-pump-dout -dout
;

\ chiller
\ 
\ 說明:
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
\ | 狀態 | forth 指令      | 說明          |
\ |------|----------------|--------------|
\ | B0   | chiller-init   | chiller init |
\ | B1   | chiller-start  | 啟動 chiller  |
\ | B2   | chiller-stop   | 關閉 chiller  |
\ 
\ Transition table
\ 
\ | 狀態  | forth 指令        | 說明   |
\ |------|------------------|--------|
\ | BT1  | tmp-high-level   | 高溫 |
\ | BT2  | tmp-low-level    | 正常 |
\ | BT3  | tmp-mid-level    | 低溫 |


\ Step

: chiller-init
  chiller-dout +dout
;
: chiller-start
  +chiller
;
: chiller-stop
  -chiller
;

\ 水溫大於上限為高準位
: tmp-high-level
  shimizu-tmp@ tmp-high-limit f@ f>
;
\ 水溫低於下限為低準位
: tmp-low-level
  shimizu-tmp@ tmp-low-limit f@ f<
;
\ 水溫低於上限 且 水溫高於下限 為中準位
: tmp-mid-level
  tmp-high-level not
  tmp-low-level not
  and
;

: chiller-true1
  true
;
: chiller-true2
  true
;

\ Step Definition
step chiller-init
step chiller-start
step chiller-stop

\ Transition Definition
transition tmp-high-level
transition tmp-mid-level
transition tmp-low-level
transition chiller-true1
transition chiller-true2

\ Link
' chiller-init       ' tmp-high-level     -->
' chiller-init       ' tmp-mid-level      -->
' chiller-init       ' tmp-low-level      -->

' tmp-high-level     ' chiller-start      -->
' tmp-mid-level      ' chiller-init       -->
' tmp-low-level      ' chiller-stop       -->
' chiller-start      ' chiller-true1      -->
' chiller-stop       ' chiller-true2      -->

' chiller-true1      ' chiller-init       -->
' chiller-true2      ' chiller-init       -->
marker -work