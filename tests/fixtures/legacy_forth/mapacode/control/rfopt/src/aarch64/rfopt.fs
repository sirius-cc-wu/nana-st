code exit
   0xa8c17bfd ,  \ ldp	x29, x30, [sp], #16
   0xd65f03c0 ,  \ ret
   0xd65f03c0 ,  \ ret
end-code 

code noop
   0xd65f03c0 ,  \ ret
end-code 

code _lit
   0xa94e0801 ,  \ ldp	x1, x2, [x0, #224]
   0xf9400029 ,  \ ldr	x9, [x1]
   0xf8008449 ,  \ str	x9, [x2], #8
   0xd65f03c0 ,  \ ret
end-code compile-only

code rp!
   0xaa1503fc ,  \ mov	x28, x21
   0xf8408775 ,  \ ldr	x21, [x27], #8
   0xd65f03c0 ,  \ ret
end-code compile-only

code sp!
   0xaa1503fb ,  \ mov	x27, x21
   0x58000015 ,  \ ldr	x21, 7f84 <sp_store+0x4>
   0xd65f03c0 ,  \ ret
end-code compile-only

code abs
   0xaa1503e9 ,  \ mov	x9, x21
   0xcb0903e9 ,  \ neg	x9, x9
   0xf10002bf ,  \ cmp	x21, #0x0
   0x9a89a2b5 ,  \ csel	x21, x21, x9, ge  // ge = tcont
   0xd65f03c0 ,  \ ret
end-code 

code dup
   0xf81f8f75 ,  \ str	x21, [x27, #-8]!
   0xd65f03c0 ,  \ ret
end-code 

