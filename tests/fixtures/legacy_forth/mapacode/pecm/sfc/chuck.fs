-work


\ ==================== chuck ====================

variable chuck-go 
variable chuck-axis
variable chuck-target-axis
variable chuck-mode         \ 0不動作 1夾緊 2放開
variable chuck-target-mode
variable GoChuck-step
variable GoChuck-count

: +chuck
    PECM-user-axis @ chuck-target-axis !
    1 chuck-target-mode !
    chuck-go on
;
: -chuck
    PECM-user-axis @ chuck-target-axis !
    2 chuck-target-mode !
    chuck-go on
;


\ sfc
\ step
: chuck-idle
    ( waiting )
;
step chuck-idle

: chuck-init
    chuck-target-axis @ chuck-axis !
    chuck-target-mode @ chuck-mode !
    1 GoChuck-step !
    0 GoChuck-count !
;
step chuck-init

: GoChuck
    GoChuck-step @
    case
        \ step1 判斷夾緊還是放開或是都沒有
        1 of
            chuck-mode @ 1 = if
            \ 夾緊則跳到step2
            2 GoChuck-step !
            else
                chuck-mode @ 2 = if
                    \ 放開則先跳到102
                    102 GoChuck-step !
                else 
                    \ 非1或2 則跳到0 結束這個流程
                    0 GoChuck-step !
                then
            then
        endof
        
        \ 此步驟為夾緊 先判斷哪個軸後,先吹氣三秒
        2 of
            chuck-axis @
            case
                1 of
                    xa-chuck-blow +dout
                endof
                2 of
                    xb-chuck-blow +dout
                endof
                3 of
                    xc-chuck-blow +dout
                endof
            endcase
            0 GoChuck-count !
            1 GoChuck-step +!
        endof

        \ 吹氣3秒
        3 of
            1 GoChuck-count +!
            GoChuck-count @ 3000 > if
                1 GoChuck-step +!
            then
        endof

        \ 關閉吹氣
        4 of
            chuck-axis @
            case
                1 of
                    xa-chuck-blow -dout
                endof
                2 of
                    xb-chuck-blow -dout
                endof
                3 of
                    xc-chuck-blow -dout
                endof
            endcase
            0 GoChuck-count !
            1 GoChuck-step +!
        endof
        
        \ 等待2秒
        5 of
            1 GoChuck-count +!
            GoChuck-count @ 2000 > if
                1 GoChuck-step +!
            then
        endof

        \ 關閉吹氣後等待2秒後 將lock打開 與 release 關閉
        6 of
            chuck-axis @
            case
                1 of
                    xa-chuck-lock +dout
                    xa-chuck-release -dout
                endof
                2 of
                    xb-chuck-lock +dout
                    xb-chuck-release -dout
                endof
                3 of
                    xc-chuck-lock +dout
                    xc-chuck-release -dout
                endof
            endcase
            0 GoChuck-count !
            1 GoChuck-step +!
        endof
        \ 等待2秒後 將check打開
        7 of
            1 GoChuck-count +!
            GoChuck-count @ 2000 > if
                1 GoChuck-step +!
            then
        endof
        \ 將check打開後 就結束
        8 of
            chuck-axis @
            case
                1 of
                    xa-chuck-check +dout
                endof
                2 of
                    xb-chuck-check +dout
                endof
                3 of
                    xc-chuck-check +dout
                endof
            endcase
            0 GoChuck-step !
        endof

        \ 以下為放開流程
        \ 將lock關閉 與 release 開起
        102 of
            chuck-axis @
            case
                1 of
                    xa-chuck-lock -dout
                    xa-chuck-release +dout
                endof
                2 of
                    xb-chuck-lock -dout
                    xb-chuck-release +dout
                endof
                3 of
                    xc-chuck-lock -dout
                    xc-chuck-release +dout
                endof
            endcase
            0 GoChuck-count !
            1 GoChuck-step +!
        endof
        \ 等待1秒後 將吹氣打開
        103 of
            1 GoChuck-count +!
            GoChuck-count @ 1000 > if
                1 GoChuck-step +!
            then
        endof
        \ 打開吹氣
        104 of
            chuck-axis @
            case
                1 of
                    xa-chuck-blow +dout
                endof
                2 of
                    xb-chuck-blow +dout
                endof
                3 of
                    xc-chuck-blow +dout
                endof
            endcase
            0 GoChuck-count !
            1 GoChuck-step +!
        endof
        \ 等待3秒後 將吹氣關閉
        105 of
            1 GoChuck-count +!
            GoChuck-count @ 1000 > if
                1 GoChuck-step +!
            then
        endof
        \ 關閉吹氣,並且不需要確認即可結束流程
        106 of
            chuck-axis @
            case
                1 of
                    xa-chuck-blow -dout
                    xa-chuck-check -dout
                endof
                2 of
                    xb-chuck-blow -dout
                    xb-chuck-check -dout
                endof
                3 of
                    xc-chuck-blow -dout
                    xc-chuck-check -dout
                endof
            endcase
            0 GoChuck-step !
        endof

        \ 此為結束
        0 of
            chuck-go off
        endof
    endcase
;
step GoChuck

\ transition
: chuck-go?
    chuck-go @
;
transition chuck-go?

: chuck-init-ready?
    true
;
transition chuck-init-ready?

: chuck-end?
    chuck-go @ not
;
transition chuck-end?
\ ==============link===============
' chuck-idle            ' chuck-go?             -->
' chuck-go?             ' chuck-init            -->
' chuck-init            ' chuck-init-ready?     -->
' chuck-init-ready?     ' GoChuck               -->
' GoChuck               ' chuck-end?            -->
' chuck-end?            ' chuck-idle            -->

marker -work