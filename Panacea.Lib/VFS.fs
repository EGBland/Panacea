namespace Panacea.Lib

open System.Reflection

module VFS =
    type Header =
        | RootDirHeader of uint32 * uint32
        | SubdirHeader  of string * uint32 * uint32
        | FileHeader    of string * uint32 * uint32 * uint64
        with
            member this.Name =
                match this with
                    | RootDirHeader _ -> "<root>"
                    | SubdirHeader (name,_,_) -> name
                    | FileHeader (name,_,_,_) -> name
            
            member this.IsFile =
                match this with
                    | FileHeader _ -> true
                    | _ -> false
            
            member this.IsDir = this.IsFile |> not
            
            member this.Size =
                match this with
                    | FileHeader (_,size,_,_) -> int size
                    | _ -> 0
            
            member this.Offset =
                match this with
                    | FileHeader (_,_,offset,_) -> int offset
                    | _ -> 0
            
            member this.Timestamp =
                match this with
                    | FileHeader (_,_,_,timestamp) -> timestamp
                    | _ -> uint64 0
            
            member this.WithName (name: string) =
                match this with
                    | FileHeader (_,size,offset,timestamp) -> FileHeader (name,size,offset,timestamp)
                    | SubdirHeader (_,numSubdirs,numFiles) -> SubdirHeader (name,numSubdirs,numFiles)
                    | RootDirHeader _ -> this
                    

    type VFS = Tree.Tree<Header>
    
    exception VFSReadException of string
    
    // TODO this should be an encoding other than UTF8 I think, also may just be able to use ReadString, also idk if VFS
    // uses varints for length since there's no examples of strings longer than 127. maybe poke the engine for this
    let private decodeVarString (reader: System.IO.BinaryReader) =
        let strLen = reader.ReadByte () |> int
        let theString = reader.ReadBytes strLen
        System.Text.Encoding.UTF8.GetString theString
    
    let private decodeFileHeader (reader: System.IO.BinaryReader) =
        let fileName      = decodeVarString reader
        let fileSize      = reader.ReadUInt32 ()
        let fileOffset    = reader.ReadUInt32 ()
        let fileTimestamp = reader.ReadUInt64 ()
        printfn $"{fileName}"
        FileHeader (fileName, fileSize, fileOffset, fileTimestamp)
    
    let rec private decodeDir (isRoot: bool) (reader: System.IO.BinaryReader): Tree.Tree<Header> =
        let mutable subdirName = ""
        if not isRoot
        then subdirName <- decodeVarString reader
        let numSubdirs = reader.ReadUInt32 ()
        let numFiles = reader.ReadUInt32 ()
        
        let thisHeader = if isRoot then RootDirHeader (numSubdirs, numFiles) else SubdirHeader (subdirName, numSubdirs, numFiles)
        
        let fileHeaders = seq { for _ in 1 .. int numFiles -> decodeFileHeader reader } |> Seq.toList |> List.map (fun x -> Tree.Branch (x,[]))
        let subdirHeaders = seq { for _ in 1 .. int numSubdirs -> decodeDir false reader } |> Seq.toList
        
        Tree.Branch (thisHeader, List.concat [fileHeaders; subdirHeaders])
        
    let decode (inputStream: System.IO.Stream) =
        let reader = new System.IO.BinaryReader(inputStream)
        let magic = reader.ReadUInt32 ()
        if magic <> uint32 0x4331504C
        then raise (VFSReadException $"Invalid magic number -- expected 0x4331504C, got 0x%08X{magic}.")
        
        decodeDir true reader
    
    let private encodeVarString (str: string) = seq {
        yield byte str.Length
        yield! System.Text.Encoding.UTF8.GetBytes str
    }
    
    let private encodeUInt32 (x: uint32) = seq {
        yield byte ( x         &&& 0x000000ffu)
        yield byte ((x >>>  8) &&& 0x000000ffu)
        yield byte ((x >>> 16) &&& 0x000000ffu)
        yield byte ((x >>> 24) &&& 0x000000ffu)
    }
    
    let private encodeUInt64 (x: uint64) = seq {
        yield byte ( x         &&& uint64 0x000000ffu)
        yield byte ((x >>>  8) &&& uint64 0x000000ffu)
        yield byte ((x >>> 16) &&& uint64 0x000000ffu)
        yield byte ((x >>> 24) &&& uint64 0x000000ffu)
        yield byte ((x >>> 32) &&& uint64 0x000000ffu)
        yield byte ((x >>> 40) &&& uint64 0x000000ffu)
        yield byte ((x >>> 48) &&& uint64 0x000000ffu)
        yield byte ((x >>> 56) &&& uint64 0x000000ffu)
    }
    
    let private encodeMagic = encodeUInt32 0x4331504Cu
    
    exception VFSEncodeException of string
    
    let private encodeHeader (header: Header): byte seq = seq {
        match header with
            | FileHeader (name,size,offset,timestamp) ->
                yield! encodeVarString name
                yield! encodeUInt32 size
                yield! encodeUInt32 offset
                yield! encodeUInt64 timestamp
            | RootDirHeader (subdirCount, fileCount) ->
                yield! encodeUInt32 <| uint32 0x4331504C
                yield! encodeUInt32 subdirCount
                yield! encodeUInt32 fileCount
            | SubdirHeader (name, subdirCount, fileCount) ->
                yield! encodeVarString name
                yield! encodeUInt32 subdirCount
                yield! encodeUInt32 fileCount
        }

    let rec private flatten (root: Tree.Tree<Header>): Header seq =
        match root with
            | Tree.Tip -> []
            | Tree.Branch (x,children) -> seq {
                yield x
                yield! Seq.concat <| Seq.map flatten children
            }
        
    exception VFSLoadException of string
    let loadFrom (inputStream: System.IO.Stream) (header: Header) =
        if header.IsFile
        then
            inputStream.Seek(int64 header.Offset, System.IO.SeekOrigin.Begin) |> ignore
            let data: byte array = Array.zeroCreate(header.Size)
            inputStream.ReadExactly(data)
            data
        else raise (VFSLoadException("Cannot load a directory.")) // TODO this message is shit
    
    let encode (vfs: Tree.Tree<Header>) (dataSource: System.IO.Stream): byte seq = seq {
            let flattened = flatten vfs
            yield! (Seq.concat <| Seq.map encodeHeader flattened)
            let allFiles = (List.filter (fun (x: Header) -> x.IsFile)) <| (Seq.toList flattened)
            let fileData = Array.concat <| List.toSeq (List.map (fun (x: Header) -> loadFrom dataSource x) allFiles)
            yield! fileData
        }