﻿namespace Hopac.Extras

open System
open Hopac
open Hopac.Job.Infixes
open Hopac.Extensions
open System.Threading.Tasks
open Hopac.Core

module JobChoice = 
  let bind (xJ: Job<Choice<'x, 'e>>) (x2yJ: 'x -> Job<Choice<'y, 'e>>) : Job<Choice<'y, 'e>> =
    xJ >>= function
    | Fail error -> Job.result <| Fail error 
    | Ok x -> x2yJ x 
  let bindAsync (xA: Async<Choice<'x, 'e>>) (x2yJ: 'x -> Job<Choice<'y, 'e>>) : Job<Choice<'y, 'e>> =
    bind (Async.toJob xA) x2yJ
  let bindTask (xT: Task<Choice<'x, 'e>>) (x2yJ: 'x -> Job<Choice<'y, 'e>>) : Job<Choice<'y, 'e>> =
    bind (Task.awaitJob xT) x2yJ
  let bindVoidTask (uT: Task) (u2xJ: unit -> Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> = 
    Task.bindJob (uT, u2xJ)

  let result (x: 'x) : Job<Choice<'x, 'e>> = Job.result <| Ok x
  let map (x2y: 'x -> 'y) (x: Job<Choice<'x, 'e>>): Job<Choice<'y, 'e>> =
    x >>= function
    | Ok x -> x2y x |>> Ok
    | Fail e -> Job.result (Fail e) 
  let mapError (e2f: 'e -> 'f) (x: Job<Choice<'x, 'e>>): Job<Choice<'x, 'f>> =
    x >>= function
    | Ok x -> Job.result <| Ok x
    | Fail e -> Job.result <| Fail (e2f e)

open JobChoice

[<Sealed>] 
type JobChoiceBuilder () =
  member __.Bind (xJ: Job<Choice<'x, 'e>>, x2yJ: 'x -> Job<Choice<'y, 'e>>) : Job<Choice<'y, 'e>> = 
    bind xJ x2yJ
  member __.Bind (xA: Async<Choice<'x, 'e>>, x2yJ: 'x -> Job<Choice<'y, 'e>>) : Job<Choice<'y, 'e>> =
    bindAsync xA x2yJ
  member __.Bind (xT: Task<Choice<'x, 'e>>, x2yJ: 'x -> Job<Choice<'y, 'e>>) : Job<Choice<'y, 'e>> =
    bindTask xT x2yJ 
  member __.Bind (uT: Task, u2xJ: unit -> Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> = 
    bindVoidTask uT u2xJ

  member __.Combine (uA: Async<Choice<unit, 'e>>, xJ: Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> =
    Async.toJob uA >>. xJ
  member __.Combine (uT: Task<Choice<unit, 'e>>, xJ: Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> =
    Task.awaitJob uT >>. xJ
  member __.Combine (uT: Task, xJ: Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> =
    Task.awaitJob uT >>. xJ
  member __.Combine (uJ: Job<Choice<unit, 'e>>, xJ: Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> =  
    uJ >>= function
    | Ok() -> xJ
    | Fail e -> Job.result <| Fail e

  member __.Delay (u2xJ: unit -> Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> = Job.delay u2xJ

  member inline __.For (xs: seq<'x>, x2uJ: 'x -> Job<Choice<unit, 'e>>) : Job<Choice<unit, 'e>> =
    Job.using (xs.GetEnumerator()) <| fun enum ->
      let rec loop() =
        if enum.MoveNext() then
          x2uJ enum.Current >>= function
          | Ok _ -> loop()
          | fail -> Job.result fail
        else Job.result <| Ok()
      loop()
   
  member __.Return (x: 'x) : Job<Choice<'x, 'e>> = result x

  member __.ReturnFrom (xA: Async<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> = Async.toJob xA
  member __.ReturnFrom (xT: Task<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> = Task.awaitJob xT
  member job.ReturnFrom (uT: Task) : Job<Choice<unit, 'e>> = Task.bindJob (uT, job.Zero)
  member __.ReturnFrom (xJ: Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> = xJ

  member __.TryFinally (xA: Async<Choice<'x, 'e>>, u2u: unit -> unit) : Job<Choice<'x, 'e>> =
    Job.tryFinallyFun (Async.toJob xA) u2u
  member __.TryFinally (xT: Task<Choice<'x, 'e>>, u2u: unit -> unit) : Job<Choice<'x, 'e>> =
    Job.tryFinallyFun (Task.awaitJob xT) u2u
  member job.TryFinally (uT: Task, u2u: unit -> unit) : Job<Choice<unit, 'e>> =
    Job.tryFinallyFun (Task.bindJob (uT, job.Zero)) u2u
  member __.TryFinally (xJ: Job<Choice<'x, 'e>>, u2u: unit -> unit) : Job<Choice<'x, 'e>> =
    Job.tryFinallyFun xJ u2u

  member __.TryWith (xA: Async<Choice<'x, 'e>>, e2xJ: exn -> Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> =
    Job.tryWith (Async.toJob xA) e2xJ
  member __.TryWith (xT: Task<Choice<'x, 'e>>, e2xJ: exn -> Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> =
    Job.tryWith (Task.awaitJob xT) e2xJ
  member job.TryWith (uT: Task, e2xJ: exn -> Job<Choice<unit, 'e>>) : Job<Choice<unit, 'e>> =
    Job.tryWith (Task.bindJob (uT, job.Zero)) e2xJ
  member __.TryWith (xJ: Job<Choice<'x, 'e>>, e2xJ: exn -> Job<Choice<'x, 'e>>) : Job<Choice<'x, 'e>> =
    Job.tryWith xJ e2xJ

  member __.Using (x: 'x when 'x :> IDisposable, x2yJ: _ -> Job<Choice<'y, 'e>>) : Job<Choice<'y, 'e>> =
    Job.using x x2yJ
     
  member job.While (u2b: unit -> bool, uJ: Job<Choice<unit, 'e>>) : Job<Choice<unit, 'e>> =
    if u2b() then
      job.Bind(uJ, (fun () -> job.While(u2b, uJ)))
    else job.Zero()

  member __.Zero () : Job<Choice<unit, 'e>> = StaticData.unit |>> Ok

[<AutoOpen>]
module TopLevel =
  let jobChoice = JobChoiceBuilder()