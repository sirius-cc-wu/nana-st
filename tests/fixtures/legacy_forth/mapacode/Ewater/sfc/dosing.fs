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

' ph-high-level     ' ph+H             -->
' ph-mid-level      ' ph-close          -->
' ph-low-level      ' ph+OH              -->
' ph+H              ' ph-true1          -->
' ph-close          ' ph-true2          -->
' ph+OH             ' ph-true3          -->

' ph-true1          ' ph-init           -->
' ph-true2          ' ph-init           -->
' ph-true3          ' ph-init           -->

\ 導電度
fvariable rc-low-limit 50e rc-low-limit f!           \ reactor-conduct 反應槽導電度下限
fvariable rc-high-limit 100e rc-high-limit f!        \ reactor-conduct 反應槽導電度上限

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
\ | B0   | rc-init          | reactor-conduct init       |
\ | B1   | rc+ro             | 加ro        |
\ | B2   | rc-close         | 導電度正常 不加藥  |
\ | B3   | rc+NaN03            | 加NaN03  |
\ 
\ Transition table
\ 
\ | 狀態  | forth 指令        | 說明   |
\ |------|------------------|--------|
\ | BT1  | rc-high-level   | 導電度過高 |
\ | BT2  | rc-low-level    | 導電度過低 |
\ | BT3  | rc-mid-level    | 導電度適中|


\ Step

: rc-init
  ( do nothing )
;

\ 自動模式下才可自動加
: rc+ro
  dosing-mode @ auto-dosing-mode = if
    -NaN03-b
    \ 目前無自動加ro
  then
;

: rc+NaN03
  dosing-mode @ auto-dosing-mode = if
    +NaN03-b
  then
;

: rc-close
  dosing-mode @ auto-dosing-mode = if
    -NaN03-b
  then
;

\ PH大於上限為高準位
: rc-high-level
  reactor-conduct@ rc-high-limit f@ f>
;
\ PH低於下限為低準位
: rc-low-level
  reactor-conduct@ rc-low-limit  f@ f<
;
\ PH低於上限 且 水溫高於下限 為中準位
: rc-mid-level
  rc-high-level not
  rc-low-level not
  and
;

: rc-true1
  true
;
: rc-true2
  true
;
: rc-true3
  true
;

\ Step Definition
step rc-init
step rc+NaN03
step rc+ro
step rc-close

\ Transition Definition
transition rc-high-level
transition rc-mid-level
transition rc-low-level
transition rc-true1
transition rc-true2
transition rc-true3

\ Link
' rc-init           ' rc-high-level     -->
' rc-init           ' rc-mid-level      -->
' rc-init           ' rc-low-level      -->

' rc-high-level     ' rc+ro             -->
' rc-mid-level      ' rc-close          -->
' rc-low-level      ' rc+NaN03          -->
' rc+ro             ' rc-true1          -->
' rc-close          ' rc-true2          -->
' rc+NaN03          ' rc-true3          -->

' rc-true1          ' rc-init           -->
' rc-true2          ' rc-init           -->
' rc-true3          ' rc-init           -->

marker -work