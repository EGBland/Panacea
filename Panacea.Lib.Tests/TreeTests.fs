namespace Panacea.Lib.Tests

open NUnit.Framework
open Panacea.Lib.Tree

module TreeTests =
    [<Test>]
    let TestMapEmpty () =
        Assert.AreEqual((Tip: Tree<int>), map ((*) 2: int -> int) Tip)

    [<Test>]
    let TestMap () =
        let treeInitial = Branch (1, [Branch (3, [Branch (4, [Branch (9,[])])]) ; Branch (2, [Branch (6, []) ; Branch (7,[])]) ; Branch (5,[])])
        let treeExpected = Branch (2, [Branch (6, [Branch (8, [Branch (18,[])])]) ; Branch (4, [Branch (12, []) ; Branch (14,[])]) ; Branch (10,[])])
        
        let treeMapped = map ((*) 2) treeInitial
        
        Assert.AreEqual(treeExpected, treeMapped)
