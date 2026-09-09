-work

\ Redefine stop, 只有 TASK 1 才執行 me suspend pause, 避免 TASK 2/3/4/5 停掉了。
: stop me 1 = if ." stop|ok" cr 1 suspend pause else ." stop|Not background task" cr then ;

\ Programming API
: end-of-program ( -- ) ." end-of-program|ok" cr ;
: abort-program ( -- )  ." end-of-program|abort by user" cr kill-task1 1 resume ;

\ EtherCAT
: pp ( -- mode ) 1 ;
: pv ( -- mode ) 3 ;
: tq ( -- mode ) 4 ;
: hm ( -- mode ) 6 ;
: csp ( -- mode ) 8 ;
: csv ( -- mode ) 9 ;
: cst ( -- mode ) 10 ;
: switch-on-disabled ( -- pds-goal ) 1 ;
: ready-to-switch-on ( -- pds-goal ) 2 ;
: switched-on ( -- pds-goal ) 3 ;
: operation-enabled ( -- pds-goal ) 4 ;
: quick-stop-active ( -- pds-goal ) 5 ;

: until-no-requests ( -- )
    ." log|until-no-requests" cr
    begin
        waiting-requests?
    while
        pause
    repeat ;
    
: until-target-reached ( channel slave -- )
    ." log|" over over swap . . ." until-target-reached" cr
    pause pause pause pause pause pause
    begin
        over over target-reached? not
    while
        pause
    repeat
    drop drop ;

: drive-on ( channel slave -- )
    operation-enabled -rot pds-goal! ;
    
: drive-off ( channel slave -- )
    switch-on-disabled -rot pds-goal! ;      

: drive-stop ( channel slave -- )
    quick-stop-active -rot pds-goal! ; 

: until-drive-on ( channel slave -- )
    ." log|" over over swap . . ." until-drive-on" cr
    pause pause pause pause pause pause
    begin
        over over drive-on? not
    while
        pause
    repeat
    drop drop ;    

: until-no-fault ( channel slave -- )     
    ." log|" over over swap . . ." until-no-fault" cr
    pause pause pause pause pause pause
    begin
        over over drive-fault?
    while
        pause
    repeat
    drop drop ; 
    
: until-grp-end ( grp -- ) 
   ." log|" dup . ." until-grp-end" cr
   begin
        dup group! gend? not
   while
        pause
   repeat
   drop ;    
   

variable laser-axis
variable laser-pause
variable laser-count
fvariable laser-feedrate
fvariable laser-step
fvariable laser-backlash

: laser-move  ( F: pos - )     
   laser-axis @ axis-cmd-p!
   begin laser-axis @ interpolator-reached? not
   while pause repeat
   ." target reached " laser-axis @ axis-demand-p@ f.
   ;

: laser-forward ( F: start-pos - start-pos )
   fdup laser-backlash f@ f- laser-move 1000 ms
   fdup laser-move laser-pause @ ms
   0 begin dup laser-count @ < while
   fdup laser-step f@ dup 1 + s>f f* f+ laser-move laser-pause @ ms 1 +
   repeat drop
   ;

: laser-backward ( F: start-pos - )
   laser-step f@ laser-count @ s>f f* f+
   fdup laser-backlash f@ f+ laser-move 1000 ms
   fdup laser-move laser-pause @ ms
   0 begin dup laser-count @ < while
   fdup laser-step f@ -1.0e f* dup 1 + s>f f* f+ laser-move laser-pause @ ms 1 +
   repeat drop fdrop
   ;

: laser-fb ( axis count ms - )( F: start-pos step feedrate backlash - )
   laser-pause ! laser-count ! laser-axis ! laser-backlash f! laser-feedrate f! laser-step f!
   laser-feedrate f@ laser-axis @ interpolator-v!
   laser-axis @ +interpolator
   laser-forward
   laser-backward
   laser-axis @ -interpolator
   ." end-of-laser-fb"
  ;

\ Suspend NC task (task1)
: suspend-nc 1 suspend ;

\ Resume NC task (task1)
: resume-nc 1 resume ;

\ Kill NC task (task1)
: kill-nc kill-task1 1 resume ;

\ Error handler
: handle-error
    0stacks error -2 1 within not if
      cr ." error|" .error  [char] ( emit  .token  [char] ) emit
    then 0error ;

\ Task 1: background task, compiles and runs large programs, ex. NC program.
variable evals
: evaluate1
    begin parse-word
        token-empty? not
    while
        compiling? if compile-token ?stacks else interpret-token ?stacks then
        1 evals +!  evals @ 80 >  if 0 evals !  pause then
    repeat ;
: quit1 reset begin leave-task pause enter-task evaluate1 again ;
: (abort1) handle-error quit1 ;

\ Task 2,3: user tasks
: evaluate
    begin parse-word
        token-empty? not
    while
        compiling? if compile-token ?stacks else interpret-token ?stacks then
    repeat ;
: quit reset begin leave-task pause enter-task evaluate again ;
: (abort) handle-error quit ;
: start-terminal ( n -- )
    activate  ['] (abort) handler! quit ;

\ Task 4: real-time event loop
: quit4 reset begin leave-task communicate pause run-control-task enter-task again ;
: (abort4)   handle-error quit4 ;
: start-task4   4 activate  ['] (abort4) handler!  quit4 ;

\ Task 5: background task, do nothing.
: quit5 reset begin leave-task pause enter-task again ;
: (abort5)   handle-error quit5 ;
: start-task5   5 activate  ['] (abort5) handler!  quit5 ;

: main
    2 start-terminal  3 start-terminal  start-task4  start-task5
    ['] (abort1) handler!  quit1 ;

marker -work
