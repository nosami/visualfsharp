namespace MonoDevelop.FSharp.Shared
open System
open System.Collections.Generic
open System.Text
open FSharp.Compiler
open FSharp.Compiler.SourceCodeServices

module Symbols =
    let getLocationFromSymbolUse (s: FSharpSymbolUse) =
        [s.Symbol.DeclarationLocation; s.Symbol.SignatureLocation]
        |> List.choose id
        |> List.distinctBy (fun r -> r.FileName)

    let getLocationFromSymbol (s:FSharpSymbol) =
        [s.DeclarationLocation; s.SignatureLocation]
        |> List.choose id
        |> List.distinctBy (fun r -> r.FileName)

[<AutoOpen>]
module SymbolUse =
    let (|ActivePatternCase|_|) (symbol : FSharpSymbolUse) =
        match symbol.Symbol with
        | :? FSharpActivePatternCase as ap-> ActivePatternCase(ap) |> Some
        | _ -> None

    let (|Entity|_|) (symbol : FSharpSymbolUse) =
        match symbol.Symbol with
        | :? FSharpEntity as ent -> Some ent
        | _ -> None

    let (|Field|_|) (symbol : FSharpSymbolUse) =
        match symbol.Symbol with
        | :? FSharpField as field-> Some field
        |  _ -> None

    let (|GenericParameter|_|) (symbol: FSharpSymbolUse) =
        match symbol.Symbol with
        | :? FSharpGenericParameter as gp -> Some gp
        | _ -> None

    let (|MemberFunctionOrValue|_|) (symbol : FSharpSymbolUse) =
        match symbol.Symbol with
        | :? FSharpMemberOrFunctionOrValue as func -> Some func
        | _ -> None

    let (|ActivePattern|_|) = function
        | MemberFunctionOrValue m when m.IsActivePattern -> Some m | _ -> None

    let (|Parameter|_|) (symbol : FSharpSymbolUse) =
        match symbol.Symbol with
        | :? FSharpParameter as param -> Some param
        | _ -> None

    let (|StaticParameter|_|) (symbol : FSharpSymbolUse) =
        match symbol.Symbol with
        | :? FSharpStaticParameter as sp -> Some sp
        | _ -> None

    let (|UnionCase|_|) (symbol : FSharpSymbolUse) =
        match symbol.Symbol with
        | :? FSharpUnionCase as uc-> Some uc
        | _ -> None

    let (|Constructor|_|) = function
        | MemberFunctionOrValue func when func.IsConstructor || func.IsImplicitConstructor -> Some func
        | _ -> None

    let (|TypeAbbreviation|_|) = function
        | Entity symbol when symbol.IsFSharpAbbreviation -> Some symbol
        | _ -> None

    let (|Class|_|) = function
        | Entity symbol when symbol.IsClass -> Some symbol
        | Entity s when s.IsFSharp &&
                        s.IsOpaque &&
                        not s.IsFSharpModule &&
                        not s.IsNamespace &&
                        not s.IsDelegate &&
                        not s.IsFSharpUnion &&
                        not s.IsFSharpRecord &&
                        not s.IsInterface &&
                        not s.IsValueType -> Some s
        | _ -> None

    let (|Delegate|_|) = function
        | Entity symbol when symbol.IsDelegate -> Some symbol
        | _ -> None

    let (|Event|_|) = function
        | MemberFunctionOrValue symbol when symbol.IsEvent -> Some symbol
        | _ -> None

    let (|Property|_|) = function
        | MemberFunctionOrValue symbol when symbol.IsProperty || symbol.IsPropertyGetterMethod || symbol.IsPropertySetterMethod -> Some symbol
        | _ -> None

    let inline private notCtorOrProp (symbol:FSharpMemberOrFunctionOrValue) =
        not symbol.IsConstructor && not symbol.IsPropertyGetterMethod && not symbol.IsPropertySetterMethod

    let (|Method|_|) (symbolUse:FSharpSymbolUse) =
        match symbolUse with
        | MemberFunctionOrValue symbol when symbol.IsModuleValueOrMember  &&
                                            not symbolUse.IsFromPattern &&
                                            not symbol.IsOperatorOrActivePattern &&
                                            not symbol.IsPropertyGetterMethod &&
                                            not symbol.IsPropertySetterMethod -> Some symbol
        | _ -> None

    let (|Function|_|) (symbolUse:FSharpSymbolUse) =
        match symbolUse with
        | MemberFunctionOrValue symbol when notCtorOrProp symbol  &&
                                            symbol.IsModuleValueOrMember &&
                                            not symbol.IsOperatorOrActivePattern &&
                                            not symbolUse.IsFromPattern ->
            match symbol.FullTypeSafe with
            | Some fullType when fullType.IsFunctionType -> Some symbol
            | _ -> None
        | _ -> None

    let (|Operator|_|) (symbolUse:FSharpSymbolUse) =
        match symbolUse with
        | MemberFunctionOrValue symbol when notCtorOrProp symbol &&
                                            not symbolUse.IsFromPattern &&
                                            not symbol.IsActivePattern &&
                                            symbol.IsOperatorOrActivePattern ->
            match symbol.FullTypeSafe with
            | Some fullType when fullType.IsFunctionType -> Some symbol
            | _ -> None
        | _ -> None

    let (|Pattern|_|) (symbolUse:FSharpSymbolUse) =
        match symbolUse with
        | MemberFunctionOrValue symbol when notCtorOrProp symbol &&
                                            not symbol.IsOperatorOrActivePattern &&
                                            symbolUse.IsFromPattern ->
            match symbol.FullTypeSafe with
            | Some fullType when fullType.IsFunctionType ->Some symbol
            | _ -> None
        | _ -> None


    let (|ClosureOrNestedFunction|_|) = function
        | MemberFunctionOrValue symbol when notCtorOrProp symbol &&
                                            not symbol.IsOperatorOrActivePattern &&
                                            not symbol.IsModuleValueOrMember ->
            match symbol.FullTypeSafe with
            | Some fullType when fullType.IsFunctionType -> Some symbol
            | _ -> None
        | _ -> None

    
    let (|Val|_|) = function
        | MemberFunctionOrValue symbol when notCtorOrProp symbol &&
                                            not symbol.IsOperatorOrActivePattern ->
            match symbol.FullTypeSafe with
            | Some _fullType -> Some symbol
            | _ -> None
        | _ -> None

    let (|Enum|_|) = function
        | Entity symbol when symbol.IsEnum -> Some symbol
        | _ -> None

    let (|Interface|_|) = function
        | Entity symbol when symbol.IsInterface -> Some symbol
        | _ -> None

    let (|Module|_|) = function
        | Entity symbol when symbol.IsFSharpModule -> Some symbol
        | _ -> None

    let (|Namespace|_|) = function
        | Entity symbol when symbol.IsNamespace -> Some symbol
        | _ -> None

    let (|Record|_|) = function
        | Entity symbol when symbol.IsFSharpRecord -> Some symbol
        | _ -> None

    let (|Union|_|) = function
        | Entity symbol when symbol.IsFSharpUnion -> Some symbol
        | _ -> None

    let (|ValueType|_|) = function
        | Entity symbol when symbol.IsValueType && not symbol.IsEnum -> Some symbol
        | _ -> None

    let (|ComputationExpression|_|) (symbol:FSharpSymbolUse) =
        if symbol.IsFromComputationExpression then Some symbol
        else None
        
    let (|Attribute|_|) = function
        | Entity ent ->
            if ent.AllBaseTypes
               |> Seq.exists (fun t ->
                                  if t.HasTypeDefinition then
                                      t.TypeDefinition.TryFullName
                                      |> Option.exists ((=) "System.Attribute" )
                                  else false)
            then Some ent
            else None
        | _ -> None

