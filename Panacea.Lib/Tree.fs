namespace Panacea.Lib

module Tree =
    type Tree<'a> =
        | Tip
        | Branch of 'a * Tree<'a> list
        with
            member this.Value =
                match this with
                    | Tip -> None
                    | Branch (x,_) -> Some x
            
            member this.Children =
                match this with
                    | Tip -> []
                    | Branch (_,children) -> children

    let rec map (f: 'a -> 'b) (tree: Tree<'a>) =
        match tree with
            | Tip -> Tip
            | Branch (x,cs) -> Branch (f x, List.map (map f) cs)
