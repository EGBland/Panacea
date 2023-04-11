namespace Panacea.Lib

module Tree =
    
    type Tree<'a> =
        | Tip
        | Branch of 'a * Tree<'a> list
        member Children: Tree<'a> list
        member Value: 'a option
    
    val map: f: ('a -> 'b) -> tree: Tree<'a> -> Tree<'b>