-work

 
\ Output SCOPE information
\ 此命令可選擇紀錄時間
: .recording ( time -- ) 
  0
  begin
      1 + 2dup >= 
  while
      ." record_postion|" mtime . 44 emit 4 axis-demand-p@ 1000000e f* f. 44 emit 4 axis-real-p@ 1000000e f* 4 1 f.r cr 
      pause 
      repeat 
  2drop 
  100 ms 
  cr ." end-of-record|ok" cr
;

\ ==================== scope ====================
\ 說明:
\       會根據輸入頻率 apk-vf, 去決定需要送出多少資料, 目前設定為2個波形的資料
\       並於送完後做一個delay的動作,目的為不想人機一直去更新資料
\       此外只有在加工中,與人機送出.scope 才會做送資料的動作
\ SFC:
\               scope-idle
\                   |
\                   +   go-fetch-data?
\                   |
\                   v
\           scope-setting-end-point
\                   |
\                   +   setting-ready?  true
\                   |
\                   v
\           scope-record-postion
\                   |
\                   +   record-ok?  >record-total-point
\                   |
\                   v
\           scope-record-end
\                   |
\                   +   scope-delay-ready? true
\                   |
\                   v
\               scope-delay
\                   |
\                   +   scope-delay-ok?
\                   |
\                   v
\               scope-idle
\
variable go-fetch-data go-fetch-data off
variable fetch-data fetch-data on
\ 此變數提供給工程師做使用,已避免api被洗頻
variable record-total-point
\ 用於顯示位置資訊
: .scope ( -- )
    fetch-data @ 
    ECM-motion-state @ machining = and
    go-fetch-data !
; 

: scope-idle ( waiting ) ;

\ 由頻率去計算應該送多少點,目前會送2個波形的點數量 
\ 例如50HZ,則會送1000 / 50 * 2 = 40 個點
: scope-setting-end-point
    1000e apk-vf@ 1e fmax f/ 2e f* f>s
    record-total-point !
    vi-trigger +dout
    ." scope_data_go|ok" cr
;

: scope-record-postion
    ." scope_data|" mtime . 44 emit 4 axis-real-p@ 1000000e f* 4 1 f.r 44 emit cur-trigger f@ 1000000e f* 4 1 f.r cr     
;
: scope-record-end
    ." scope_data_end|ok"
    vi-trigger -dout
    go-fetch-data off
;
: scope-delay ( waiting ) ;

: go-fetch-data? go-fetch-data @ ;
: setting-ready? true ;
: record-ok? ['] scope-record-postion elapsed record-total-point @ >  ;
: scope-delay-ready? true ;
: scope-delay-ok? ['] scope-delay elapsed 3000 > ;

step scope-idle
step scope-setting-end-point
step scope-record-postion
step scope-record-end
step scope-delay
transition go-fetch-data?
transition setting-ready?
transition record-ok?
transition scope-delay-ready?
transition scope-delay-ok?

' scope-idle                ' go-fetch-data? -->
' go-fetch-data?            ' scope-setting-end-point -->
' scope-setting-end-point   ' setting-ready? -->
' setting-ready?            ' scope-record-postion -->
' scope-record-postion      ' record-ok? -->
' record-ok?                ' scope-record-end -->
' scope-record-end          ' scope-delay-ready? -->
' scope-delay-ready?        ' scope-delay -->
' scope-delay               ' scope-delay-ok? -->
' scope-delay-ok?           ' scope-idle -->


marker -work