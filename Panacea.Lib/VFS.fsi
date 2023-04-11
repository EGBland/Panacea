namespace Panacea.Lib

module VFS =
    
    type Header =
        | RootDirHeader of uint32 * uint32
        | SubdirHeader of string * uint32 * uint32
        | FileHeader of string * uint32 * uint32 * uint64
        
        member Name: string
        member IsFile: bool
        member IsDir: bool
        member Size: int
        member Offset: int
        member WithName: name: string -> Header
    type VFS = Tree.Tree<Header>
    
    exception VFSReadException of string
    
    val private decodeVarString: reader: System.IO.BinaryReader -> string
    
    val private decodeFileHeader: reader: System.IO.BinaryReader -> Header
    
    val private decodeDir:
      isRoot: bool -> reader: System.IO.BinaryReader -> Tree.Tree<Header>
    
    val decode: inputStream: System.IO.Stream -> Tree.Tree<Header>
    val encode: vfs: Tree.Tree<Header> -> dataSource: System.IO.Stream -> byte seq
    
    val loadFrom: inputStream: System.IO.Stream -> header: Header -> byte array

