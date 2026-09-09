-work

\ 這裡放置 SFC 通用指令。

\ Fetch float parameter
: fparam@ ( index addr -- ) ( F: --  value )
  faligned swap floats + f@
;

\ Set float parameter
: fparam! ( index addr -- ) ( F: value -- )
  faligned swap floats + f!
;

\ Fetch parameter
: param@ ( index addr -- value )
  swap cells + @
;

\ Set parameter
: param! ( value index addr -- )
  swap cells + !
;

\ 將旗標反向
: not! ( addr -- )
  dup @ not swap !
;

\ 與變數值 and 後存入
: and! ( val addr -- )
  dup @ rot and swap !
;

\ 與變數值 or 後存入
: or! ( val addr -- )
  dup @ rot or swap !
;

\ Renew clock
: renew ( clock-addr -- )
  mtime swap !
;

\ Return if the clock time has elapsed 't'.
: elapsed>? ( clock-addr t -- flag )
  mtime rot @ - <
;

\ 若 n 大於 addr 存的值就覆蓋
: max! ( n addr -- )
  dup @ rot max swap !
;

marker -work