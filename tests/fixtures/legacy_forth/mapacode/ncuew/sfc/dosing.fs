-work

fvariable ph-low-limit 5e ph-low-limit f!           \ ph下限
fvariable ph-high-limit 10e ph-high-limit f!        \ ph上限

\ ph-dosing
\ 
\ 說明:
\ 
\ SFC 邏輯圖:
\ 
\            BT1 +----+ TRUE
\          +--+--+ B1 +--+---> B0
\          |     +----+
\ +----+   | BT2 +----+ TRUE
\ | B0 +---+--+--+ B2 +--+---> B0
\ +----+   |     +----+
\          | BT3 +----+ TRUE
\          +--+--+ B3 +--+---> B0
\                +----+
\ 
\ Step table
\ 
\ | 狀態  | forth 指令       | 說明          |
\ |------|------------------|--------------|
\ | B0   | ph-init          | ph init       |
\ | B1   | ph+H             | 加HNO3        |
\ | B2   | ph-close         | 酸鹼正常 不加藥  |
\ | B3   | ph+OH            | 加NaOH  |
\ 
\ Transition table
\ 
\ | 狀態  | forth 指令        | 說明   |
\ |------|------------------|--------|
\ | BT1  | ph-high-level   | PH過高 |
\ | BT2  | ph-low-level    | ph過低 |
\ | BT3  | ph-mid-level    | ph適中|


\ Step

: ph-init
  ( do nothing )
;

\ 自動模式下才可自動加
: ph+H
  dosing-mode @ auto-dosing-mode = if
    -NaOH-b
    +HNO3-b
  then
;

: ph+OH
  dosing-mode @ auto-dosing-mode = if
    +NaOH-b
    -HNO3-b
  then
;

: ph-close
  dosing-mode @ auto-dosing-mode = if
    -NaOH-b
    -HNO3-b
  then
;

\ PH大於上限為高準位
: ph-high-level
  reactor-ph@ ph-high-limit f@ f>
;
\ PH低於下限為低準位
: ph-low-level
  reactor-ph@ ph-low-limit  f@ f<
;
\ PH低於上限 且 水溫高於下限 為中準位
: ph-mid-level
  ph-high-level not
  ph-low-level not
  and
;

: ph-true1
  true
;
: ph-true2
  true
;
: ph-true3
  true
;

\ Step Definition
step ph-init
step ph+OH
step ph+H
step ph-close

\ Transition Definition
transition ph-high-level
transition ph-mid-level
transition ph-low-level
transition ph-true1
transition ph-true2
transition ph-true3

\ Link
' ph-init           ' ph-high-level     -->
' ph-init           ' ph-mid-level      -->
' ph-init           ' ph-low-level      -->

' ph-high-level     ' ph+H              -->
' ph-mid-level      ' ph-close          -->
' ph-low-level      ' ph+OH             -->
' ph+H              ' ph-true1          -->
' ph-close          ' ph-true2          -->
' ph+OH             ' ph-true3          -->

' ph-true1          ' ph-init           -->
' ph-true2          ' ph-init           -->
' ph-true3          ' ph-init           -->

marker -work