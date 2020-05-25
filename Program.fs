module HelloGiraffe.App

open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Giraffe.Serialization
open LiteDB
open LiteDB.FSharp
open Newtonsoft.Json
open Microsoft.FSharpLu.Json
open HelloGiraffe.Types
open FSharp.Control.Tasks.V2.ContextInsensitive

open HelloGiraffe.JsonSerializerSettings

// ---------------------------------
// Views
// ---------------------------------

module Views =
    open GiraffeViewEngine

    let layout (content: XmlNode list) =
        html [] [
            head [] [
                title []  [ encodedText "HelloGiraffe" ]
                link [ _rel  "stylesheet"
                       _type "text/css"
                       _href "/main.css" ]
            ]
            body [] content
        ]

    let partial () =
        h1 [] [ encodedText "HelloGiraffe" ]

    let index (model : Message) =
        [
            partial()
            p [] [ encodedText model.Text ]
        ] |> layout

// ---------------------------------
// Web app
// ---------------------------------

let indexHandler (name : string) =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let greetings = sprintf "Hello %s, from Giraffe!" name
        let model     = { Text = greetings }
        let view      = Views.index model

        let albums = ctx.GetService<LiteDatabase>().GetCollection<Album>("albums")
        let metallica = {
            Id = 0;
            Name = name;
            Genre = Metal;
            DateReleased = DateTime.Now
        }
        albums.Insert(metallica) |> ignore
        printfn "%A" metallica
        htmlView view next ctx

let animalGreetingHandler () =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let name =
            ctx.TryGetQueryStringValue "name"
            |> Option.defaultValue "Giraffe"
        let greeting = sprintf "Hello World, from %s" name
        json { Text = greeting } next ctx

let animalsHandler (count : int) =
    let animals = [
        { Animal.Type = AnimalDog
          FirstName = "Mikan"
          LastName = "Apple"
          Age = 3
          DateOfBirth = System.DateTime.Now
      };
        { Animal.Type = AnimalCat
          FirstName = "Rosie"
          LastName = "Bubblegum"
          Age = 5
          DateOfBirth = System.DateTime.Now
      };
        { Animal.Type = AnimalGiraffe
          FirstName = "Gerard"
          LastName = "Banana"
          Age = 7
          DateOfBirth = System.DateTime.Now
      }
    ]
    json animals.[0..count-1]

let getAlbumsHandler (id : int) =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let albums = ctx.GetService<LiteDatabase>().GetCollection<Album>("albums")
        let result = albums.FindById(BsonValue(id))
        json result next ctx

let getAlbumsListHandler () =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let albums = ctx.GetService<LiteDatabase>().GetCollection<Album>("albums")
        let result = albums.FindAll()
        json result next ctx

let putAlbumsHandler (newAlbum : NewAlbum) =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let albums = ctx.GetService<LiteDatabase>().GetCollection<Album>("albums")
            albums.Insert(toAlbum(newAlbum)) |> ignore
            printfn "%A" newAlbum
            return! Successful.ok (json {| Result = "Success!" |}) next ctx
        }

let deleteAlbumsHandler (id : int) =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let albums = ctx.GetService<LiteDatabase>().GetCollection<Album>("albums")
        let result = albums.Delete(BsonValue(id))
        json result next ctx

let webApp =
    choose [
        GET >=>
            choose [
                route "/" >=> indexHandler "world"
                routef "/hello/%s" indexHandler
                route "/giraffe-greeting" >=> animalGreetingHandler ()
                routef "/animals/%i" animalsHandler
                routef "/albums/%i" getAlbumsHandler
                route "/albums" >=> getAlbumsListHandler ()
            ]
        PUT >=>
            choose [
                route "/albums" >=> bindJson<NewAlbum> putAlbumsHandler
            ]
        DELETE >=>
            choose [
                routef "/albums/%i" deleteAlbumsHandler
            ]
        setStatusCode 404 >=> text "Not Found" ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureCors (builder : CorsPolicyBuilder) =
    builder.WithOrigins("http://localhost:8080")
           .AllowAnyMethod()
           .AllowAnyHeader()
           |> ignore

let configureApp (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
    (match env.EnvironmentName = "Development" with
    | true  -> app.UseDeveloperExceptionPage()
    | false -> app.UseGiraffeErrorHandler errorHandler)
        .UseHttpsRedirection()
        .UseCors(configureCors)
        .UseStaticFiles()
        .UseGiraffe(webApp)

let configureServices (services : IServiceCollection) =
    services.AddCors()    |> ignore
    services.AddGiraffe() |> ignore

    let mapper = FSharpBsonMapper()
    services.AddTransient<LiteDatabase>(fun _ ->
        new LiteDatabase("Filename=simple.db; Mode=Exclusive", mapper)
    ) |> ignore

    let customSettings = JsonSerializerSettings()
    customSettings.Converters.Add(CompactUnionJsonConverter(true))
    customSettings.ContractResolver <- RequireAllPropertiesContractResolver()

    services.AddSingleton<IJsonSerializer>(
        NewtonsoftJsonSerializer(customSettings)) |> ignore

let configureLogging (builder : ILoggingBuilder) =
    builder.AddFilter(fun l -> l.Equals LogLevel.Error)
           .AddConsole()
           .AddDebug() |> ignore

[<EntryPoint>]
let main _ =
    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot     = Path.Combine(contentRoot, "WebRoot")
    WebHostBuilder()
        .UseKestrel()
        .UseContentRoot(contentRoot)
        .UseIISIntegration()
        .UseWebRoot(webRoot)
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0
