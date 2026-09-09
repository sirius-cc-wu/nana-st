-work

\ Is motion inited ?
variable motion-inited
: .motion-inited ." motion-inited|" motion-inited @ . cr ;
: init-motion! motion-inited ! ;

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



\ ===== 加藥模式 =====
0 constant auto-dosing-mode                               \ 自動加藥模式
1 constant manual-dosing-mode                             \ 手動加藥模式
variable dosing-mode  auto-dosing-mode dosing-mode !      \ 預設自動加藥模式

marker -work