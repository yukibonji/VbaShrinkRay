﻿(**
# VBA Shrink-Ray!

Being able to easily and regularly visualize how much **legacy code** exists
in your application's codebase - as [Dan Milstein][dm] describes in his article
[Screw you Joel Spolsky, We're Rewriting It From Scratch!][rw]; is a key tool
in measuring success.

This is a script that helps treat a codebase like data - for use in doing a
**re-write**; and targets `VBA` code in a reasonably naive way. But should 
still remain valuable.
 
 [dm]: https://twitter.com/danmil
 [rw]: http://onstartups.com/tabid/3339/bid/97052/Screw-You-Joel-Spolsky-We-re-Rewriting-It-From-Scratch.aspx
*)

(*** hide ***)
open System
open System.IO
open System.Text.RegularExpressions

(**
## VBA

First things first - we need some types to represent the domain of `VBA` code.

To begin with I'm **really not** going to go too mad. I can actually work 
towards leaning quite a lot of information with just a few small data types:
*)

/// Represents a line of code - which as we know can be only either executable
/// code or a line of commenting.
type lineOfCode = 
    | Code of string
    | Comment of string

/// Represent bindings of args, parameters, variables as a name
/// and optional type information.
type allocation = Allocation of string * string option

/// Record of information about a procedure call.
type procedureInfo = { 
    
    /// The name bound to the procudure.
    Name: string
    
    /// The optional list of arguments required to call the procedure.
    Args: allocation list option
      
    /// All of the lines of code that make up the procedure, inclusive of the
    /// procedure decleration, ending and comments.
    LinesOfCode: lineOfCode seq
}

/// Available types of procedure calls - procedures are either Sub-Procedures
/// or they are Function-Procedures.
type procedure =    
    | Sub of procedureInfo
    | Function of procedureInfo

(**
With just these few types we can probably generate the following information
pretty easily:

* Number of lines of `Code`.
* Number of lines of `Comment`.
* Total number of lines of code.
* Total number of `Sub` and `Function` procedures.
* Total number of calls to each `Sub` and `Function` procedures.
* Details of the arguments required by both `Sub` and `Function` procedures.
*)

(** 
### Helpers

I was initially going to set the following helper functions to hidden - but in 
retrospect I'm going to leave them in; I think it us useful to see how I arrive
at my solution with a little explanation of intent etc.

First up are a couple of helper functions to let me work with `option` types in
a slightly more elegant way, reducing the code noise.
*)

/// Applies a function 'f' to an option 'a', where 'a' has a value and returns
/// some value - else the result is obviously nothing.
let maybe f a = 
    match a with
    | Some b -> Some (f b)
    | None -> None

/// Turns a collection into an optional collection - where an empty collection
/// is represented as nothing.
let isAny xs = if Seq.isEmpty xs then None else Some xs

(** 
Given that we're essentially parsing large bodies of text and trying work with
them as if it is data - we're going to be needing a few helper functions for
working with strings. A few are just functional wrappers over existing string
methods, so we can more easily use functional idions like [partial application][pa]
and [function composition][fc].

 [pa]: http://fsharpforfunandprofit.com/posts/convenience-partial-application/
 [fc]: http://fsharpforfunandprofit.com/posts/function-composition/
*)

/// Normalize line endings in a string.
let normalizeLineEndings ending s =
    Regex.Replace(input=s, pattern="\r\n|\n\r|\n|\r", replacement=ending)

/// Splits a string based on a an array of given splitter strings. By default
/// we will remove all empty entries from the result set.
let split (splitters:string[]) (s:string) = 
    s.Split(separator=splitters, options=StringSplitOptions.RemoveEmptyEntries)
    |> Seq.ofArray

/// Trims both ends of a specified string.
let trim (s:string) = s.Trim()

/// Trims both ends of all strings in a specified collection.
let trimAll xs = Seq.map trim xs

/// Results in true of a given string starts with a specified string value.
let startsWith find (s:string) = 
    s.StartsWith(value=find, comparisonType=StringComparison.CurrentCultureIgnoreCase)

/// Removes all empty empty strings from a given collection, resulting in an
/// optional collection if there are still any values in it.
let removeEmpty = (Seq.filter (String.IsNullOrWhiteSpace >> not)) >> isAny

(**
### Transformation

So now we have a few helper functions to get us underway with working with 
our values - we can start making sense of constructing our domain model from
the data we're provided; transforming the raw strings into something we can 
more easily work with.
*)

/// Naive line tokenizer that breaks a string into the separate lines.
///
/// TODO: VB syntax allows the use of the '_' character to specify that a line
/// logically continues onto the next physical line. We should probably handle
/// this in the future.
let tokenizeLines = 
    
    // We will use the standard windows line endings for this.
    let windowsLineEnding = "\r\n"

    normalizeLineEndings windowsLineEnding
    >> split [|windowsLineEnding|]
    >> trimAll
    >> removeEmpty
    
(**
Not all lines of code are created equal - some are executable, and some are 
purely commenting. 

We'll need another function that can take each line we have and work out what
they actually are.
*)

/// VB syntax defines the ' character as a way to mark a comment follows. Since
/// all lines (including executable ones) can have a ' in them, we can at this
/// point only say that a line starting with a ' are purely a line of comment.
let lineOfCode s = if startsWith "'" s then Comment s else Code s

/// Map function to turn a collection of strings into an optional collection of
/// lines of code.
let linesOfCode = tokenizeLines >> maybe (Seq.map lineOfCode)

(**
I've come quite far now and don't want go too much further without documenting
a couple of test cases so we can see some of this basic transformation working.
This will help us trust/reason what is going on with later functions.

I've been using the [F# Interactive][fsi] (the REPL for F# programming) to test
the behaviour of each of these functions so far - I won't bore you with the 
output of all the testing for all of the functions; but I will demonstrate the
output from using the `linesOfCode` function.

 [fsi]: http://msdn.microsoft.com/en-us/library/dd233175.aspx

Here is a little test helper function that takes a *test name* and then the 
value to pass into the `linesOfCode` function, interrogate the results and
print them nicely to the screen.

What follows is a few test runs with a few different scenarios:
*)

/// Takes a string as input and runs it through our 'linesOfCode' function
/// and prints the results in what should be nice and readable.
let ``test linesOfCode`` str name =
    printfn "Test: %s" name
    match linesOfCode str with
    | None -> printfn "WARN! No lines of code found."
    | Some lines ->        
        Seq.iteri (fun i l -> 
            printfn "Line %i: %A" (i+1) l
        ) lines
    printfn "" // for some clarity.

// Example 1
"Empty strings should yield no lines of code." 
|> ``test linesOfCode`` ""

// Example 2
"A few lines of whitespace should yield no lines of code." 
|> ``test linesOfCode`` """


"""

// Example 3
"One line statement should yield 1 line of code."
|> ``test linesOfCode`` "Dim s = \"Hello, world!\""

// Example 4
"A 3 lines with a comment, should yield 2 lines of code and 1 comment"
|> ``test linesOfCode`` """
    Sub ZeroArgumentSubProcedure()
        'does nothing
    End Sub
"""

(**
Here are the test results from the output in the REPL: 

    Test: Empty strings should yield no lines of code.
    WARN! No lines of code found.

    Test: A few lines of whitespace should yield no lines of code.
    WARN! No lines of code found.

    Test: One line statement should yield 1 line of code.
    Line 1: Code "Dim s = "Hello, world!""

    Test: A 3 lines with a comment, should yield 2 lines of code and 1 comment
    Line 1: Code "Sub ZeroArgumentSubProcedure()"
    Line 2: Comment "'does nothing"
    Line 3: Code "End Sub"

Excellent! - that seems to be working great. It'll break if we deliberately 
throw some `null`'s at it - but I plan to ensure that won't happen as I read
the data in, so shouldn't be the case here.

At this point I think we can start getting some meaningful data already. We 
should now very easily be able to calculate the total numbers of lines of
code in a given codebase:

* Number of lines of `Code`.
* Number of lines of `Comment`.
* Total number of lines.

That's already 50% of our original set of requirements above!

This can probably be done with some simple [map/reduce][mr]:

 [mr]: http://fsharpforfunandprofit.com/posts/monoids-part2/
*)

/// Counts the number of executable lines of code.
let codeLineCount lines = 
    lines
    |> Seq.map (fun line ->
        match line with
        | Code _ -> 1
        | Comment _ -> 0
    )
    |> Seq.sum

/// Counts the number of comment lines.
let commentLineCount lines = 
    lines
    |> Seq.map (fun line ->
        match line with
        | Code _ -> 0
        | Comment _ -> 1
    )
    |> Seq.sum

/// Counts total number of lines of code.
let totalLineCount lines = 
    lines
    |> Seq.map (fun line -> 1)
    |> Seq.sum

(**
I'm going to redefine my `test` function above so as I can prove these few
map/reduce functions are correct:
*)

/// Takes a string as input and runs it through our 'linesOfCode' function
/// and prints the results in what should be nice and readable.
let ``test aggregate functions`` str name =
    printfn "Test: %s" name
    match linesOfCode str  with
    | None -> printfn "WARN! No lines of code found."
    | Some lines -> 
        lines |> codeLineCount |> (printfn "Number of lines of Code: %i") 
        lines |> commentLineCount |> (printfn "Number of lines of Comments: %i") 
        lines |> totalLineCount |> (printfn "Total: %i") 
    printfn "" // for some clarity.

// Example 1
"Empty strings should yield no lines of code." 
|> ``test aggregate functions`` ""

// Example 2
"A few lines of whitespace should yield no lines of code." 
|> ``test aggregate functions`` """


"""

// Example 3
"One line statement should yield 1 line of code."
|> ``test aggregate functions`` "Dim s = \"Hello, world!\""

// Example 4
"A 3 lines with a comment, should yield 2 lines of code and 1 comment and 3 total."
|> ``test aggregate functions`` """
    Sub ZeroArgumentSubProcedure()
        'does nothing
    End Sub
"""

(**
The resulting output in our REPL:

    Test: Empty strings should yield no lines of code.
    WARN! No lines of code found.

    Test: A few lines of whitespace should yield no lines of code.
    WARN! No lines of code found.

    Test: One line statement should yield 1 line of code.
    Number of lines of Code: 1
    Number of lines of Comments: 0
    Total: 1

    Test: A 3 lines with a comment, should yield 2 lines of code and 1 comment and 3 total.
    Number of lines of Code: 2
    Number of lines of Comments: 1
    Total: 3
*)














(*** hide ***)
let tokenizeWords = 
    split [|" "|] >> trimAll

(*** hide ***)
let tokenize vb =
    tokenizeLines vb |> maybe (Seq.map tokenizeWords)

(*** hide ***)
let getProcedures vb = 
    option<procedure seq>.None