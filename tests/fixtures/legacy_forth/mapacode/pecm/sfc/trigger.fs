-work

\ ==================== trigger ====================
\ 說明:
\   監聽控制面板上的 start, pause, reset 及遙控器上的 start 按鍵是否被按下, 並處理
\   各自的觸發條件和觸發行為
\
\ SFC:
\              trigger-init
\                   |
\                   + trigger-init-done?
\                   |
\                   v
\              trigger-idle
\                   |
\                   + trigger-start?
\                   |
\                   v
\             trigger-forth
\                   |
\                   + trigger-end?
\                   |
\                   v
\             trigger-init
\

1 constant panel-start
2 constant panel-pause
3 constant panel-reset
4 constant remoter-start

variable trigger-init-done
variable trigger-job
variable trigger-step           \ step 從 1 開始, 0 表示工作結束
variable panel-start-triggered

\ 回傳訊息告訴人機是否該送出工件程式
: .panel-start ( -- )
    panel-start-triggered @
    if
        ." panel_start|true" cr
    else
        ." panel_start|false" cr
    then
;

\ 若 trigger-job = 0 表示 trigger 閒置中, 就設定 job
: trigger-job! ( job -- )
    trigger-job @ 0=
    if
        trigger-job !
    else
        drop
    then
;

\ 搜尋需要處理的 job
: ?trigger-job ( -- )
    trigger-job @ 0=                \ 若沒有被指派工作
    if
        start-ch-slv ec-din@        \ control panel 上的 start 按鍵
        if
            panel-start trigger-job !
            exit
        then
        pause-ch-slv ec-din@        \ control panel 上的 pause 按鍵
        if
            panel-pause trigger-job !
            exit
        then
        reset-ch-slv ec-din@        \ control panel 上的 reset 按鍵
        if
            panel-reset trigger-job !
            exit
        then
    then
;



\ -------------------- steps --------------------
\ 初始化
: trigger-init ( -- )
    0 trigger-job !                 \ 清除 job
    1 trigger-step !

    trigger-init-done on
;
step trigger-init

\ 閒置中, 檢查有沒有需要處理的 job
: trigger-idle ( -- )
    ?trigger-job
;
step trigger-idle

\ 處理 trigger-job 對應的工作
: trigger-forth ( -- )
    trigger-job @
    case
        panel-start of
            trigger-step @
            case
                \ 初始化
                1 of
                    panel-start-hold 0timer     \ 更新 timer
                    1 trigger-step +!           \ 切換至下一步
                endof

                \ 等待按鍵按住並進行 panel start
                2 of
                    panel-start-hold timer-expired? \ 若已按住足夠長的時間
                    if
                        PECM-motion-state @ idle =       \ 若 motion idle
                        feedhold? not and               \ 且不是 feedhold
                        if
                            panel-start-triggered on        \ 開啟 panel start triggered 旗標
                        else
                            restart-motion                  \ restart-motion
                        then
                        1 trigger-step +!               \ 切換至下一步
                    else
                        start-ch-slv ec-din@ not    \ 若已放開按鍵
                        if
                            0 trigger-step !                \ 清除 trigger step 以離開 trigger forth
                        then
                    then
                endof

                \ 若 motion not idle 表示可能人機已送出 NC 程式，或已 restart-motion，或有其它操作介入，
                \ 則關閉 panel-start-triggered 旗標後切換到下一步，或是按鍵放開了也切換到下一步離開
                3 of
                    PECM-motion-state @ idle <>  \ 若 motion not idle
                    start-ch-slv ec-din@ not or \ 或按鍵已放開
                    if
                        panel-start-triggered off   \ 關閉 panel start triggered 旗標
                        1 trigger-step +!           \ 切換至下一步
                    then
                endof

                \ 等待按鍵放開後離開 trigger forth
                \ 這裡要拆出來等待是因為有可能上一步是因為 motion not idle 而切過來，這種情況下還要確保按鍵放開才離開
                4 of
                    start-ch-slv ec-din@ not
                    if
                        0 trigger-step !            \ 清除 trigger step 以離開 trigger forth
                    then
                endof
            endcase
        endof

        panel-pause of
            trigger-step @
            case
                \ 初始化
                1 of
                    panel-pause-hold 0timer     \ 更新 timer
                    1 trigger-step +!           \ 切換至下一步
                endof

                \ 等待按鍵按住並進行 panel pause
                2 of
                    panel-pause-hold timer-expired? \ 若已按住足夠長的時間
                    if
                        stop-motion                     \ stop motion
                        1 trigger-step +!               \ 切換至下一步
                    else
                        pause-ch-slv ec-din@ not    \ 若已放開按鍵
                        if
                            0 trigger-step !                \ 清除 trigger step 以離開 trigger forth
                        then
                    then
                endof

                \ 等待按鍵放開並離開 trigger forth
                3 of
                    pause-ch-slv ec-din@ not
                    if
                        0 trigger-step !                \ 清除 trigger step 以離開 trigger forth
                    then
                endof
            endcase
        endof

        panel-reset of
            trigger-step @
            case
                \ 初始化
                1 of
                    panel-reset-hold 0timer     \ 更新 timer
                    1 trigger-step +!           \ 切換至下一步
                endof

                \ 等待按鍵按住並進行 panel reset
                2 of
                    panel-reset-hold timer-expired? \ 若已按住足夠長的時間
                    if
                        abort-motion                    \ abort motion
                        1 trigger-step +!               \ 切換至下一步
                    else
                        reset-ch-slv ec-din@ not    \ 若已放開按鍵
                        if
                            0 trigger-step !                \ 清除 trigger step 以離開 trigger forth
                        then
                    then
                endof

                \ 等待按鍵放開並離開 trigger forth
                3 of
                    reset-ch-slv ec-din@ not
                    if
                        0 trigger-step !                \ 清除 trigger step 以離開 trigger forth
                    then
                endof
            endcase
        endof

        remoter-start of
            trigger-step @
            case
                \ 進行 remoter start
                1 of
                    PECM-motion-state @ idle =   \ 若 motion idle
                    feedhold? not and           \ 且不為 feedhold
                    if
                        panel-start-triggered on    \ 開啟 panel start triggered 旗標
                        remoter-start-delay 0timer  \ 更新 timer
                        1 trigger-step +!           \ 切換至下一步
                    else
                        restart-motion              \ restart motion
                        0 trigger-step !            \ 清除 trigger step 以離開 trigger forth
                    then
                endof

                \ 等待延遲並離開 trigger forth
                2 of
                    remoter-start-delay timer-expired? \ 若已延遲足夠長的時間
                    PECM-motion-state @ idle <> or \ 或 motion not idle 
                    if
                        panel-start-triggered off   \ 關閉 panel start triggered 旗標
                        0 trigger-step !            \ 清除 trigger step 以離開 trigger forth
                    then
                endof
            endcase
        endof

        0 trigger-step !            \ 未規劃的 job, 離開 trigger-forth
    endcase
;
step trigger-forth



\ -------------------- transitions --------------------
\ 初始化完成?
: trigger-init-done? ( -- flag )
    trigger-init-done @
;
transition trigger-init-done?

\ 已接受 job?
: trigger-start? ( -- flag )
    trigger-job @ 0<>
;
transition trigger-start?

\ job 已處理完成?
: trigger-end? ( -- flag )
    trigger-step @ 0=
;
transition trigger-end?



\ -------------------- links --------------------
' trigger-init  ' trigger-init-done? --> ' trigger-init-done? ' trigger-idle  -->
' trigger-idle  ' trigger-start?    --> ' trigger-start?    ' trigger-forth -->
' trigger-forth ' trigger-end?      --> ' trigger-end?      ' trigger-init  -->

marker -work