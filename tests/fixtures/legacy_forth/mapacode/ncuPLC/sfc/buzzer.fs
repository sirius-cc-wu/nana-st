-work

\ ==================== buzzer ====================
\ 說明:
\   目前提供五種提示音供選擇，
\       常駐連續音(Continuous Beep)
\       常駐單長音(Continuous Solid Beep)
\       單長音(Single Solid Beep)
\       三短音(Three Flash Beep)
\       單短音(Single Flash Beep)
\   外部可以自由選擇要發出那一種提示音的請求，但統一由此 SFC 管理蜂鳴器的行為。
\   需注意，若發出了常駐提示的請求，請求者需主動關閉請求，若是單次提示的請求，
\   buzzer SFC 完成提示後會自動關閉請求。處理常駐提示請求時，不接受單次提示
\   請求。
\
\ 範例:
\   宣告一個 buzzer 並將其提示音設定為 Three Flash Beep
\
\       variable my-buzzer
\       three-flash-beep my-buzzer !
\
\   發出提示請求
\
\       my-buzzer set-buzzer
\
\   將其提示音改為 Continuous Solid Beep
\
\       continuous-solid-beep my-buzzer !
\
\   發出提示請求
\
\       my-buzzer set-buzzer
\
\   事件消失後關閉提示請求
\
\       my-buzzer unset-buzzer
\
\
\ SFC:
\              buzzer-init
\                   |
\                   + buzzer-init-done?
\                   |
\                   v
\           select-buzzer-mode
\                   |
\                   + select-buzzer-mode-done?
\                   |
\                   v
\           switch-buzzer-mode
\                   |
\                   + switch-buzzer-mode-done?
\                   |
\                   v
\              buzzer-pause
\                   |
\                   + buzzer-pause-done?
\                   |
\                   v
\              buzzer-forth
\                   |
\                   + buzzer-forth-done?
\                   |
\                   v
\           select-buzzer-mode
\

\ \ buzzer modes
\ continuous notice modes
variable continuous-beep
variable continuous-solid-beep

\ single notice modes
variable single-solid-beep
variable three-flash-beep
variable single-flash-beep

variable no-beep

variable buzzer-init-done
variable buzzer-speaking
variable beep-number
variable beep-count
variable selected-buzzer-mode
variable current-buzzer-mode
variable current-mode-forth-xt

\ 設置蜂鳴器 ex: touch-buzzer set-buzzer
: set-buzzer ( buzzer-addr -- )
    @ dup no-beep =
    if
        drop
    else
        1 swap +!
        1 single-flash-beep +!          \ 避免快速的 set/unset buzzer 導致要求沒被看到，留下一個 single flash beep 提示
    then
;

\ 取消設置蜂鳴器 ex: touch-buzzer unset-buzzer
: unset-buzzer ( buzzer-addr -- )
    @
    dup continuous-beep =
    over continuous-solid-beep = or     \ 為 continuous notice mode 才需要 unset
    if
        dup @ 1- 0 max swap !
    else
        drop
    then
;

\ buzzer on
: buzzer-on ( -- )
    +buzzer
    buzzer-speaking on
;

\ buzzer off
: buzzer-off ( -- )
    -buzzer
    buzzer-speaking off
;

\ initialize buzzer
: init-buzzer ( -- )
    0 beep-count !
    ff-buzzer-speaking-hl reset-ff
    ff-buzzer-speaking-ll reset-ff
;

\ 設定蜂鳴器 on/off 持續週期數，這裡週期是指 buzzer SFC 做完一次循環的時間，約為 100ms
: buzzer-on-off-n! ( on-n off-n -- )
    period-us@ * ff-buzzer-speaking-ll ff-hold!
    period-us@ * ff-buzzer-speaking-hl ff-hold!
;

\ continuous beep forth
: continuous-beep-forth ( -- )
    0 single-solid-beep !           \ 清空 single notice mode 的要求，避免累積多個要求
    0 three-flash-beep !
    0 single-flash-beep !
;

\ continuous solid beep forth
: continuous-solid-beep-forth ( -- )
    ff-buzzer-speaking-hl ff-triggered-uc?  \ 若 buzzer on 逾時
    if
        buzzer-off                              \ 關閉蜂鳴器
    else
        ff-buzzer-speaking-ll ff-triggered-uc?  \ 若 buzzer off 逾時
        if
            buzzer-on                               \ 開啟蜂鳴器
        then
    then

    0 single-solid-beep !           \ 清空 single notice mode 的要求，避免累積多個要求
    0 three-flash-beep !
    0 single-flash-beep !
;

\ single solid beep forth
: single-solid-beep-forth ( -- )
    ff-buzzer-speaking-hl ff-triggered-uc?  \ 若 buzzer on 逾時
    if
        buzzer-off                              \ 關閉蜂鳴器
        0 single-solid-beep !                   \ 清空要求
    then

    0 three-flash-beep !            \ 清空較低等級的要求，避免累積多個要求
    0 single-flash-beep !
;

\ three flash beep forth
: three-flash-beep-forth ( -- )
    ff-buzzer-speaking-hl ff-triggered-uc?  \ 若 buzzer on 逾時
    if
        buzzer-off                              \ 關閉蜂鳴器
        1 beep-count +!                         \ counter+1

        beep-count @ beep-number @ =            \ 若 beep count 到達
        if
            0 three-flash-beep !                    \ 清空要求
        then
    else
        ff-buzzer-speaking-ll ff-triggered-uc?  \ 若 buzzer off 逾時
        if
            buzzer-on                               \ 開啟蜂鳴器
        then
    then

    0 single-flash-beep !           \ 清空較低等級的要求，避免累積多個要求
;

\ single flash beep forth
: single-flash-beep-forth ( -- )
    ff-buzzer-speaking-hl ff-triggered-uc?  \ 若 buzzer on 逾時
    if
        buzzer-off                              \ 關閉蜂鳴器
        0 single-flash-beep !                   \ 清空要求
    then
;

\ no beep forth
: no-beep-forth ( -- )
    \ do nothing
;



\ -------------------- steps --------------------
\ 初始化
: buzzer-init ( -- )
    no-beep selected-buzzer-mode !
    no-beep current-buzzer-mode !
    ['] no-beep-forth current-mode-forth-xt !

    buzzer-init-done on
;
step buzzer-init

\ 按優先順序選擇蜂鳴器模式
: select-buzzer-mode ( -- )
    continuous-beep @ 0>
    if
        continuous-beep selected-buzzer-mode !
        exit
    then
    continuous-solid-beep @ 0>
    if
        continuous-solid-beep selected-buzzer-mode !
        exit
    then
    single-solid-beep @ 0>
    if
        single-solid-beep selected-buzzer-mode !
        exit
    then
    three-flash-beep @ 0>
    if
        three-flash-beep selected-buzzer-mode !
        exit
    then
    single-flash-beep @ 0>
    if
        single-flash-beep selected-buzzer-mode !
        exit
    then
    no-beep selected-buzzer-mode !
;
step select-buzzer-mode

\ 根據 selected-buzzer-mode 和 current-buzzer-mode 判斷是否要做切換的工作
: switch-buzzer-mode ( -- )
    selected-buzzer-mode @ current-buzzer-mode @ <>
    if
        selected-buzzer-mode @
        case
            continuous-beep of
                buzzer-on                               \ 開啟蜂鳴器
                continuous-beep current-buzzer-mode !   \ 切換為 continuous beep
                ['] continuous-beep-forth current-mode-forth-xt !
            endof

            continuous-solid-beep of
                buzzer-on                               \ 開啟蜂鳴器
                init-buzzer                             \ initialize buzzer
                10 10 buzzer-on-off-n!                  \ 設定 on/off 週期數
                continuous-solid-beep current-buzzer-mode ! \ 切換為 continuous solid beep
                ['] continuous-solid-beep-forth current-mode-forth-xt !
            endof

            single-solid-beep of
                buzzer-on                               \ 開啟蜂鳴器
                init-buzzer                             \ initialize buzzer
                10 10 buzzer-on-off-n!                  \ 設定 on/off 週期數
                1 beep-number !                         \ 設定 beep number 為 1
                single-solid-beep current-buzzer-mode ! \ 切換為 single solid beep
                ['] single-solid-beep-forth current-mode-forth-xt !
            endof

            three-flash-beep of
                buzzer-on                               \ 開啟蜂鳴器
                init-buzzer                             \ initialize buzzer
                1 3 buzzer-on-off-n!                    \ 設定 on/off 週期數
                3 beep-number !                         \ 設定 beep number 為 3
                three-flash-beep current-buzzer-mode !  \ 切換為 three flash beep
                ['] three-flash-beep-forth current-mode-forth-xt !
            endof

            single-flash-beep of
                buzzer-on                               \ 開啟蜂鳴器
                init-buzzer                             \ initialize buzzer
                1 3 buzzer-on-off-n!                    \ 設定 on/off 週期數
                1 beep-number !                         \ 設定 beep number 為 1
                single-flash-beep current-buzzer-mode ! \ 切換為 single flash beep
                ['] single-flash-beep-forth current-mode-forth-xt !
            endof

            no-beep of
                buzzer-off                              \ 關閉蜂鳴器
                no-beep current-buzzer-mode !           \ 切換為 no beep
                ['] no-beep-forth current-mode-forth-xt !
            endof

            ." runtime_error|Unreachable case in switch-buzzer-mode." cr
        endcase
    then

    buzzer-period 0timer                    \ 初始化 timer
;
step switch-buzzer-mode

\ buzzer pause
: buzzer-pause ( -- )
    \ do nothing
;
step buzzer-pause

\ 更新蜂鳴器狀態
: buzzer-forth ( -- )
    buzzer-speaking @ dup                   \ 更新 on/off flip flops
    ff-buzzer-speaking-hl ff-forth-uc
    ff-buzzer-speaking-ll ff-forth-uc

    current-mode-forth-xt @ execute         \ current mode forth
;
step buzzer-forth



\ -------------------- transitions --------------------
\ init done?
: buzzer-init-done? ( -- flag )
    buzzer-init-done @
;
transition buzzer-init-done?

\ select buzzer mode done?
: select-buzzer-mode-done? ( -- flag )
    true
;
transition select-buzzer-mode-done?

\ switch buzzer mode done?
: switch-buzzer-mode-done? ( -- flag )
    true
;
transition switch-buzzer-mode-done?

\ 等待逾時?
: buzzer-pause-done? ( -- flag )
    buzzer-period timer-expired?
;
transition buzzer-pause-done?

\ buzzer forth done?
: buzzer-forth-done? ( -- flag )
    true
;
transition buzzer-forth-done?



\ -------------------- links --------------------
' buzzer-init        ' buzzer-init-done?        --> ' buzzer-init-done?        ' select-buzzer-mode -->

' select-buzzer-mode ' select-buzzer-mode-done? --> ' select-buzzer-mode-done? ' switch-buzzer-mode -->
' switch-buzzer-mode ' switch-buzzer-mode-done? --> ' switch-buzzer-mode-done? ' buzzer-pause       -->
' buzzer-pause       ' buzzer-pause-done?       --> ' buzzer-pause-done?       ' buzzer-forth       -->
' buzzer-forth       ' buzzer-forth-done?       --> ' buzzer-forth-done?       ' select-buzzer-mode -->



marker -work