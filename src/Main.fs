module Main

open Elmish
open Elmish.React

Program.mkSimple State.init State.update View.render
|> Program.withReactSynchronous "app"
|> Program.run
