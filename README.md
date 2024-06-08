# Firebasic

A compiler for my own peculiar programming language that resembles Visual Basic, but has support for lower-level features such as pointers.
It uses another project of mine, NIR, to convert code into an intermediate representation, which is optimized and then dealt with by individual backends.

## Requirements:
- nasm (for assembling x64 code)
- gcc (for linking the object files together)

## Usage:
```
firebasic <input files | flags> <output file>
Flags: /win (passes -mwindows to linker)
```

## Example program
```vb
' Random number generator in Firebasic
' nokiiaa, 2022

Const NULL As Void* = 0

Declare Function Lib "user32.dll" MessageBoxA(hWnd as Void*, lpText as Char*, lpCaption as Char*, uType as UInteger) As Integer

Function Rand(state As UInteger*) As UInteger
    Dim x As UInteger = *state
    x = x Xor (x << 13)
    x = x Xor (x >> 17)
    x = x Xor (x << 5)
    *state = x
    return x
End Function

Sub Itoa(n As Integer, output As Char*)
    Dim digits As Integer = 0
    Dim i As Integer = 0
    Dim number As Integer = n
    
    While number
        digits += 1
        number /= 10
    End While
    
    output(digits) = 0
    
    While n
        i += 1
        output(digits - i) = n Mod 10 + 0x30
        n /= 10
    End While
End Sub

Function Main As Integer
    Dim state As UInteger = 244
    Dim message As Char* = "Your random number: __________."
        
    While True
        Itoa(Rand(&state) Mod 1000, message + 20)
        MessageBoxA(NULL, message, "Here it is! (wow)", 0)
    End While
    
    Return 0
End Function
```