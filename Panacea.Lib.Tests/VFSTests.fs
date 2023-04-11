namespace Panacea.Lib.Tests
open NUnit.Framework
open Panacea.Lib.VFS

module VFSTests =
    let testVFSSeq = seq { Panacea.Lib.Tests.Data.TestVFSFiles.test1; Panacea.Lib.Tests.Data.TestVFSFiles.test2 }
    [<Test>]
    let TestBadMagic () =
        let inputData: byte array = [| byte 0xEF; byte 0xBE; byte 0xAD; byte 0xDE |]
        let inputDataAsStream = new System.IO.MemoryStream(inputData)
        
        let resultException = Assert.Throws<VFSReadException> (fun () -> decode inputDataAsStream |> ignore)
        
        Assert.AreEqual(resultException.Message, "VFSReadException \"Invalid magic number -- expected 0x4331504C, got 0xDEADBEEF.\"")
    
    [<Test>]
    let TestDecodeVfs1 () =
        let inputData = Panacea.Lib.Tests.Data.TestVFSFiles.test1
        let inputDataAsStream = new System.IO.MemoryStream(inputData)
        
        let result = decode inputDataAsStream
        let file1Node = result.Children.Item 0
        
        Assert.AreEqual(result.Children.Length, 2)
        Assert.IsTrue(file1Node.Value.IsSome)
        Assert.AreEqual(file1Node.Value.Value.Name, "file1.txt")
        Assert.IsTrue((result.Children.Item 1).Value.IsSome)
        Assert.AreEqual((result.Children.Item 1).Value.Value.Name, "file2.txt")
    
    [<Test>]
    let TestLoadVfs1 () =
        let inputData = Panacea.Lib.Tests.Data.TestVFSFiles.test1
        let inputDataAsStream = new System.IO.MemoryStream(inputData)
        
        let result = decode inputDataAsStream
        let file1Node = result.Children.Item 0
        let file1Data =
            match file1Node.Value with
                | None   -> [|  |]
                | Some x -> Panacea.Lib.VFS.loadFrom inputDataAsStream x
        
        let file1Str = System.Text.Encoding.UTF8.GetString(file1Data)
        
        Assert.IsTrue(file1Node.Value.IsSome)
        Assert.AreEqual(file1Node.Value.Value.Name, "file1.txt")
        Assert.AreEqual(file1Str, "This is a file.")
        
    
    [<Test>]
    let TestVfs2 () =
        let inputData = Panacea.Lib.Tests.Data.TestVFSFiles.test2
        let inputDataAsStream = new System.IO.MemoryStream(inputData)
        
        let result = decode inputDataAsStream
        
        Assert.Pass ()
    
    [<Test>]
    let TestEncodeInverseDecode () =
        let inputData = Panacea.Lib.Tests.Data.TestVFSFiles.test1
        let inputDataAsStream = new System.IO.MemoryStream(inputData)
        
        let decoded = decode inputDataAsStream
        let encoded = Array.ofSeq <| encode decoded inputDataAsStream
        
        Assert.AreEqual(inputData, encoded)