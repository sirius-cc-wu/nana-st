-work

variable dldr-go
variable dldr-op
0 constant dldr-none
1 constant dldr-on
2 constant dldr-off

variable dldr-start-step
variable dldr-count

variable dldr-end-buzzer single-solid-beep dldr-end-buzzer !

: dldr-idle
    ( do nothing )
;
step dldr-idle

: dldr-init
    1 dldr-start-step !
    0 dldr-count !
;
step dldr-init

: dldr-start-forth
    dldr-start-step @ 
    case
        1 of
            \ 1 dldr-start-step +!                   \ 切換至下一步
            dldr-on dldr-op @ = if
                11 dldr-start-step !
            then
            dldr-off dldr-op @ = if
                21 dldr-start-step !
            then
            dldr-none dldr-op @ = if
                2 dldr-start-step !
            then
        endof

        2 of
            dldr-go off
            0 dldr-start-step !
            dldr-end-buzzer set-buzzer
        endof

        \ on的流程
        11 of
            6 200e mm/min jog-v! -0.2e 3 group! 2 pcs-p@ f- 6 jog
            1 dldr-start-step +!
            0 dldr-count !
        endof

        \ 等待判定移動
        12 of
            1 dldr-count +!
            dldr-count @ 3000 > if
                1 dldr-start-step +!
            then
        endof
        13 of
            \ 等待移動完畢
            job-stop? if
                1 dldr-start-step +!
            then
        endof
        14 of
            5 200e mm/min jog-v! -0.2e 3 group! 1 pcs-p@ f- 5 jog
            1 dldr-start-step +!
            0 dldr-count !
        endof
        \ 等待判定移動
        15 of
            1 dldr-count +!
            dldr-count @ 3000 > if
                1 dldr-start-step +!
            then
        endof
        16 of
            \ 等待移動完畢
            job-stop? if
                2 dldr-start-step !
            then
        endof

        \ off的流程
        21 of
            5 200e mm/min jog-v! 3 group! 1 pcs-p@ -1e f* 5 jog
            1 dldr-start-step +!
            0 dldr-count !
        endof

        \ 等待判定移動
        22 of
            1 dldr-count +!
            dldr-count @ 3000 > if
                1 dldr-start-step +!
            then
        endof
        23 of
            \ 等待移動完畢
            job-stop? if
                1 dldr-start-step +!
            then
        endof
        24 of
            6 200e mm/min jog-v! 3 group! 2 pcs-p@ -1e f* 6 jog
            1 dldr-start-step +!
            0 dldr-count !
        endof
        \ 等待判定移動
        25 of
            1 dldr-count +!
            dldr-count @ 3000 > if
                1 dldr-start-step +!
            then
        endof
        26 of
            \ 等待移動完畢
            job-stop? if
                2 dldr-start-step !
            then
        endof
    endcase
;
step dldr-start-forth


: dldr-go? ( -- flag )
    dldr-go @
;
transition dldr-go?

: dldr-init-done? ( -- flag )
    true
;
transition dldr-init-done?

: dldr-end? ( -- flag )
    0 dldr-start-step @ =
;
transition dldr-end?


\ ==================== links ====================
' dldr-idle             ' dldr-go?          --> ' dldr-go?          ' dldr-init        -->
' dldr-init             ' dldr-init-done?   --> ' dldr-init-done?   ' dldr-start-forth -->
' dldr-start-forth      '  dldr-end?        --> ' dldr-end?         ' dldr-idle        -->
marker -work
