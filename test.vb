Function Mul(a As Integer, b As Integer) As Integer
    a *= b
    Return a
End Function

Structure Str
    Raw As Char*
    Function Length As Integer
        Dim n As Integer = 0
        Dim ptr As Char* = Raw
        While *ptr
            ptr += 1
            n += 1
        End While
        Return n
	End Function
End Structure

Function Main As Integer
    Dim hello As Str
    hello.Raw = "Hello, world!"
    
    Return Mul(123, 456) + hello.Length()
End Function