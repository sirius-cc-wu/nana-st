-work

variable $cnc-counter

variable run-sfc-action-once


: cnc-led-blink ( -- )
  1 $cnc-counter +!
  $cnc-counter @ 250 <= if
    +all-led
  else
    -all-led
    $cnc-counter @ 500 >= if
      0 $cnc-counter !
    then
  then
;

variable $force-3phase   FALSE $force-3phase !
: force-3phase! ( flag -- ) $force-3phase ! ;

\ 確認三相電源
: 3phase-err? ( -- flag )
  3phase@ not $force-3phase @ or
;

\ ------------------------------------------------------------------------------
\ System on/off
\ ------------------------------------------------------------------------------
\ 
\ 說明:
\ 
\ 1. 正常流程:
\ 
\   1.1: 按下 System On 按鈕，檢查與 EtherCAT 連線是否成功。
\     1.1.1: 連線失敗: 閃爍 System Ready 燈, 並持續檢查與 EtherCAT 連線狀態。
\     1.1.2: 連線成功: 亮起 System Ready 與三色燈-紅燈號。
\   1.2: (1.1.1) 狀態中，按下 System Off 鈕，進行 System Off 程序。
\   1.3: (1.1.2) 狀態中，按下 System Off 鈕，若為 Power On 狀態，則先進行 
\       Drive Off，進入 Power Off 狀態，再執行 System Off。
\   1.4: (1.1.2) 狀態中，未按下 System off 鈕，若三相電源異常，則亮起三色燈-紅。
\ 
\ 2. 異常流程:
\ 
\   2.1: 按下 System On 按鈕，一直未進入 cnc ready，則長按 System Off 強制關機。
\ 
\ Graphcis:
\ 
\ ```
\ +----+ TRUE +----+  T1 ++----++  T2 ++----++
\ | S0 |--+---| S1 |--+--|| M1 ||--+--|| M2 ||
\ +----+      +----+     ++----++     ++----++
\ ```

\ Step and Transition Table:
\ 
\ 
\ Step
\ 
\ | 狀態 | forth 指令     | 說明                 |
\ |------|----------------|----------------------|
\ | S0   | sys-init       | system on/off init   |
\ | S1   | cnc-not-ready  | cnc not ready action |
\ | M1   | cnc-ready      | cnc ready action     |
\ | M2   | sys-off-action | system off action    |
\ 
\ Transition
\ 
\ | 狀態 | forth 指令       | 說明                       |
\ |------|------------------|----------------------------|
\ | T1   | check-cnc?       | 檢查 cnc ready             |
\ | T2   | sys-off-request? | 檢查是否進入 sys-off       |
\ 
\ ------------------------------------------------------------------------------

\ Step Activity
: sys-init
  ( do nothing )
;

: cnc-not-ready
  cnc-led-blink
;

\ Transition
: into-cnc-not-ready
  TRUE
;

step sys-init
step cnc-not-ready
transition into-cnc-not-ready

' sys-init start-sfc
' sys-init            ' into-cnc-not-ready     -->
' into-cnc-not-ready  ' cnc-not-ready          -->



\ ------------------------------------------------------------------------------
\ M1: CNC ready。M2: System off action
\ ------------------------------------------------------------------------------
\ 
\ M1: cnc ready
\ 
\ 說明:
\ 
\ 1. 正常流程:
\ 
\   1.1: (1.1.2) 狀態中，執行所有 SFC 後，亮起 System Ready 與三色燈-黃燈號。
\       若三相電源異常，則亮起三色燈-紅。
 
\ Graphcis:
\ 
\ 
\ ```
\   T1 +------+ TRUE +------+
\ --+--| M0-1 |--+---| M0-2 |
\      +------+      +------+
\ ```
\ 
\ M2: system off action
\ 
\ 說明:
\ 
\ 1. 正常流程:
\ 
\   1.1: (1.1.2) 狀態中，按下 System Off 鈕，若為 Power On 狀態，則先進行 
\       Drive Off，進入 Power Off 狀態，再執行 System Off。
\ 
\ ```
\   T2 +------+ TRUE +------+
\ --+--| M1-1 |--+---| M1-2 |
\      +------+      +------+
\ ```
\ 
\ 
\ Step and Transition Table:
\ 
\ 
\ Step
\ 
\ | 狀態 | forth 指令     | 說明                     |
\ |------|----------------|--------------------------|
\ | M0-1 | run-sfc-action | system off action        |
\ | M0-2 | cnc-ready      | cnc ready action         |
\ | M1-1 | sys-off-action | 送出 system off I/O 訊號 |
\ | M1-2 | shutdown       | exit motion server       |
\ 
\ Transition
\ 
\ | 狀態 | forth 指令       | 說明                       |
\ |------|------------------|----------------------------|
\ | T1   | check-cnc?       | 檢查 cnc ready             |
\ | T2   | sys-off-request? | 檢查是否進入 sys-off       |
\ 
\ ------------------------------------------------------------------------------


\ Step
: run-sfc-action
  run-sfc-action-once @ not
  if
    \ 啟動相關 SFC 程序
    \ ['] aqua-idle +step
    \ Buffer tank
    ['] buft-init +step
    \ 開啟冰水機偵測
    ['] chiller-init +step
    \ 過濾逆洗程序
    ['] aqua-idle +step
    \ 循環槽液位輸出
    ['] cycle-water-low-digital-out +step
    \ 加藥機
    \ ['] NaN03-pump-init +step
    \ 開啟ph加藥機制
    ['] ph-init +step

    cnc-ready-delay 0timer
    run-sfc-action-once on
    ." run-sfc-action " cr
  then
;

variable cnc-ready-done
: cnc-ready
  \ 因為 cnc-ready 後，會一直停在此 step，避免一直重覆執行，
  \ 使用 cnc-ready-done 旗標，避開會重覆執行，引響其他程序。
  dirty-water-process
  shimizu-water-process
  aqua-state-process
  cnc-ready-done @ not if

    +buft-run
    +NaN03-pump-run
    3phase-err? if
      +r-led
    else
      +y-led
    then
    meter-pw-dout +dout \ 暫定放這邊
    ew-ready-dout +dout \ 初始值為ready
    system-ready on
    TRUE cnc-ready-done !
  then
;

: sys-off-action
  +sys-off
;

: shutdown
  ." exit motion server" cr
  bye
;

\ Transition
: check-cnc?
  ec-ready?
;

: >cnc-ready
  cnc-ready-delay timer-expired?
;

: sys-off-request?
  check-sys-off?
;

: >shutdown?
  TRUE
;


step cnc-ready
step run-sfc-action
step sys-off-action
step shutdown


transition check-cnc?
transition >cnc-ready
transition sys-off-request?
transition >shutdown?


\ cnc ready porcess
' cnc-not-ready        ' check-cnc?          -->
' check-cnc?           ' run-sfc-action      -->
' run-sfc-action       ' >cnc-ready          -->
' >cnc-ready           ' cnc-ready           -->
\ aqua-off-action 狀態後, 才可在執行 sys-off
' filter-washback-off-action       ' sys-off-request?    -->
' sys-off-request?     ' sys-off-action      -->
' sys-off-action       ' >shutdown?          -->
' >shutdown?           ' shutdown            -->
\ 若 cnc 一直未備妥，則等待強制關機。
marker -work
