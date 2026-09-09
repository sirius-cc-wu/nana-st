-work

\ Is motion inited ?
variable motion-inited
: .motion-inited ." motion-inited|" motion-inited @ . cr ;
: init-motion! motion-inited ! ;

\ axis positive software limit
create axis-psl falign 7 floats allot

\ axis negative software limit
create axis-nsl falign 7 floats allot

\ axis software limit enabled
\ TODO: defaut to $FF
create axis-sl-enabled here 7 cells dup allot $0 fill

\ fetch axis parameter
: axis-param@ ( axis-no addr -- value )
    swap cells + @ ; 

\ Set axis parameter
: axis-param! ( value axis-no addr -- )
    swap cells + ! ;

\ fetch axis float parameter
: axis-fparam@ ( axis-no addr -- ) ( F: --  fvalue )
    faligned swap floats + f@ ;

\ Set axis float parameter
: axis-fparam! ( axis-no addr -- ) ( F: fvalue -- )
    faligned swap floats + f! ;

\ Get axis positive software limit
: axis-psl@ ( axis-no -- ) ( F: -- psl )
  axis-psl axis-fparam@ ;

\ Get axis negative software limit
: axis-nsl@ ( axis-no -- ) ( F: -- nsl )
  axis-nsl axis-fparam@ ;

\ Set axis positive software limit
: axis-psl! ( axis-no -- ) ( F: psl -- )
  axis-psl axis-fparam! ;

\ Set axis negative software limit
: axis-nsl! ( axis-no -- ) ( F: nsl -- )
  axis-nsl axis-fparam! ;

\ Set axis software limit enabled
: axis-sl-enabled! ( flag axis-no -- )
  axis-sl-enabled axis-param! ;

\ Fetch axis software enabled
: axis-sl-enabled@ ( axis-no -- flag )
  axis-sl-enabled axis-param@ ;

\ Enable axis software limit  
: +axis-sl ( axis-no - )
   true swap axis-sl-enabled! ;

\ Disable axis software limit
: -axis-sl ( axis-no - )
   false swap axis-sl-enabled! ;

\ On axis positive software limit
: on-axis-psl ( axis-no -- flag ) ( F: pos -- )
   dup axis-sl-enabled@ if axis-psl@ f> else drop fdrop false then ;

\ On axis negative software limit
: on-axis-nsl ( axis-no -- flag ) ( F: pos -- )
  dup axis-sl-enabled@  if axis-nsl@ f< else drop fdrop false then ;

\ feedrate
fvariable rapid-traverse-rate
fvariable dry-run-feedrate
fvariable tracing-rate

: rapid-traverse-rate! rapid-traverse-rate f! ;
: dry-run-feedrate! dry-run-feedrate f! ;
: tracing-rate! tracing-rate f! ;

: rapid-traverse-rate@ rapid-traverse-rate f@ ;
: dry-run-feedrate@ dry-run-feedrate f@ ;
: tracing-rate@ tracing-rate f@ ;

\ TODO: Init by parameter
100.0e mm/min rapid-traverse-rate!



\ ===== timers =====
1 constant panel-start-hold         10   panel-start-hold     timer-dur-ms!
2 constant panel-pause-hold         10   panel-pause-hold     timer-dur-ms!
3 constant panel-reset-hold         2000 panel-reset-hold     timer-dur-ms!
4 constant remoter-start-delay      200  remoter-start-delay  timer-dur-ms!
5 constant aqua-pre-start
6 constant aqua-delay-stop
7 constant feed-transmission-period
8 constant measure-on/off-delay     1000 measure-on/off-delay timer-dur-ms!
9 constant cnc-ready-delay          10000 cnc-ready-delay     timer-dur-ms!
10 constant buzzer-period           100  buzzer-period        timer-dur-ms!



\ ===== flip-flops =====
1 constant hl-trigger             \ high level trigger
2 constant ll-trigger             \ low level trigger
3 constant re-trigger             \ rising-edge trigger
4 constant fe-trigger             \ falling-edge trigger

1 constant ff-ems-re                    \ 緊急停止按鈕 rising edge
  re-trigger ff-ems-re ff-type!
2 constant ff-touch-re                  \ 碰邊 rising edge
  re-trigger ff-touch-re ff-type!
3 constant ff-touch-fe                  \ 碰邊 falling edge
  fe-trigger ff-touch-fe ff-type!
4 constant ff-short-re                  \ 量測短路 rising edge
  re-trigger ff-short-re ff-type!
5 constant ff-short-fe                  \ 量測短路 falling edge
  fe-trigger ff-short-fe ff-type!
6 constant ff-touch-ignore-re           \ 忽略碰邊 rising edge
  re-trigger ff-touch-ignore-re ff-type!
7 constant ff-touch-ignore-fe           \ 忽略碰邊 falling edge
  fe-trigger ff-touch-ignore-fe ff-type!
8 constant ff-touch-ignore-expired-hl   \ 忽略碰邊逾時 high level
  hl-trigger ff-touch-ignore-expired-hl ff-type!
  10 1000000 * ff-touch-ignore-expired-hl ff-hold!
9 constant ff-buzzer-speaking-hl        \ buzzer speaking high level
  hl-trigger ff-buzzer-speaking-hl ff-type!
10 constant ff-buzzer-speaking-ll       \ buzzer speaking low level
  ll-trigger ff-buzzer-speaking-ll ff-type!



\ ===== ECM 軟體極限設定 =====
\ XA, XB, XC, W
1 200e mm axis-psl!
1 0e mm axis-nsl!
1 +axis-sl

2 100e mm axis-psl!
2 -100e mm axis-nsl!
2 +axis-sl

3 100e mm axis-psl!
3 -100e mm axis-nsl!
3 +axis-sl

4 363e mm axis-psl!
4 -3e mm axis-nsl!
4 +axis-sl



\ ===== 加工進給模式 =====
0 constant safe-mode
1 constant normal-mode
2 constant ignore-mode
variable feed-mode                        \ 短路進給模式
variable arc-feed-mode                    \ 電弧進給模式
variable feed-short                       \ 此為 PLC 內部變數，勿放入參數頁面
fvariable feed-ratio-delta                \ 進給調變百分比變化量
fvariable feed-ratio-min                  \ 進給調變百分比下限
fvariable feed-ratio                      \ 進給調變百分比
1e feed-ratio f!
fvariable feed-ratio-old
1e feed-ratio-old f!
fvariable trace-to-hold-margin            \ 拉起後返回暫停點保留距離

\ 設定進給模式 ( 安全模式:mode=0 | 一般模式:mode=1 | 忽略短路模式:mode=2 )
: feed-mode! ( mode -- )
  \ 判斷是否為有效的設定值
  dup safe-mode =
  over normal-mode = or
  over ignore-mode = or
  if
      feed-mode !
      feed-transmission-period 0timer
      false feed-short !
  else
      drop
      ." error|Unsported feed-mode. ;A1545"
  then
;
\ 預設為一般模式
ignore-mode feed-mode!

\ 回傳進給模式設定值
: .feed-mode
  ." feed-mode|" feed-mode @ . cr
;

\ 設定電弧進給模式 ( 安全模式:mode=0 | 一般模式:mode=1 | 忽略短路模式:mode=2 )
: arc-feed-mode! ( mode -- )
  \ 判斷是否為有效的設定值
  dup safe-mode =
  over normal-mode = or
  over ignore-mode = or
  if
      arc-feed-mode !
      feed-transmission-period 0timer
      false feed-short !
  else
      drop
      ." error|Unsported Arc feed-mode. ;A1546"
  then
;
\ 預設為忽略模式
ignore-mode arc-feed-mode!

\ 回傳進給模式設定值
: .arc-feed-mode
  ." arc-feed-mode|" arc-feed-mode @ . cr
;

\ 設定進給調變週期，規範設定值為 2ms ~ 1000ms
: feed-transmission-period-ms! ( period:ms -- )
  2 max 1000 min feed-transmission-period timer-dur-ms!
;
\ 預設為 500ms
500 feed-transmission-period-ms!

\ 設定進給調變百分比變化量，規範設定值為 0% ~ 20%
: feed-ratio-delta! ( F: rate: 0.0e ~ 0.2e -- )
  0e fmax 0.2e fmin
  feed-ratio-delta f!
;
\ 預設為 5%
0.05e feed-ratio-delta!

\ 設定進給調變百分比下限，規範設定值為 0% ~ 100%
: feed-ratio-min! ( F: ratio-min: 0.0e ~ 1.0e -- )
  0e fmax 1.0e fmin
  feed-ratio-min f!
;
\ 預設為 30%
0.3e feed-ratio-min!



\ ===== Remoter jog velocity matrix =====
create RMT-velocity falign
  0.6e mm/min f, 6e mm/min f, 60e mm/min f, 200e mm/min f, 500e mm/min f, 900e mm/min f,
  0.6e mm/min f, 6e mm/min f, 60e mm/min f, 200e mm/min f, 500e mm/min f, 900e mm/min f,
  0.6e mm/min f, 6e mm/min f, 60e mm/min f, 100e mm/min f, 400e mm/min f, 800e mm/min f,
  0.6e mm/min f, 6e mm/min f, 60e mm/min f, 100e mm/min f, 400e mm/min f, 800e mm/min f,
  0.6e mm/min f, 6e mm/min f, 60e mm/min f, 100e mm/min f, 200e mm/min f, 300e mm/min f,
  0.6e mm/min f, 6e mm/min f, 60e mm/min f, 100e mm/min f, 200e mm/min f, 300e mm/min f,

: RMT-jog-velocity@ ( mode-no axis-no -- ) ( F: -- velocity )
  dup 0> over 5 < and
  if
    swap dup 0> over 7 < and
    if
      1- swap 1- 6 * + floats RMT-velocity faligned + f@
    else
      drop drop
      ." error|remoter:Invalid mode number. ;A1601"
    then
  else
    drop drop
    ." error|remoter:Invalid axis number. ;A1602"
  then
;

: RMT-jog-velocity! ( mode-no axis-no -- ) ( F: velocity -- )
  dup 0> over 5 < and
  if
    swap dup 0> over 7 < and
    if
      0e fmax 1000e mm/min fmin
      1- swap 1- 6 * + floats RMT-velocity faligned + f!
    else
      drop drop fdrop
      ." error|remoter:Invalid mode number. ;A1601"
    then
  else
    drop drop fdrop
    ." error|remoter:Invalid axis number. ;A1602"
  then
;

marker -work