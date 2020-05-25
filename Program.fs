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
        htmlView view next ctx

let animalsHandler (count : int) =
    let animals : List<Animal> = [
        { Type = Dog
          FirstName = "Mikan"
          LastName = "Apple"
          Age = 3
          DateOfBirth = System.DateTime.Parse "2017-05-20"
      };
        { Type = Cat
          FirstName = "Rosie"
          LastName = "Bubblegum"
          Age = 5
          DateOfBirth = System.DateTime.Parse "2015-03-02"
      };
        { Type = Giraffe
          FirstName = "Gerard"
          LastName = "Banana"
          Age = 7
          DateOfBirth = System.DateTime.Parse "2013-08-30"
      }
    ]
    json animals.[0..count-1]

let getAlbumListHandler () =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let albums = ctx.GetService<LiteDatabase>().GetCollection<Album>("albums")
        let result = albums.FindAll()
        json result next ctx

let getAlbumHandler (id : int) =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let albums = ctx.GetService<LiteDatabase>().GetCollection<Album>("albums")
        let result = albums.FindById(BsonValue(id))
        json result next ctx

let putAlbumHandler (newAlbum : NewAlbum) =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let albums = ctx.GetService<LiteDatabase>().GetCollection<Album>("albums")
        let album = {
            Id = 0
            Name = newAlbum.Name
            DateReleased = System.DateTime.Now
            Genre = newAlbum.Genre
        }
        albums.Insert(album) |> ignore
        printfn "%A" album
        let result = json {| Result = "Album created"; Id = album.Id |}
        Successful.created result next ctx

let postAlbumHandler (id : int) (updatedAlbum : Album) =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let albums = ctx.GetService<LiteDatabase>().GetCollection<Album>("albums")
        let album = { updatedAlbum with Id = id }
        albums.Update(album) |> ignore
        printfn "%A" album
        let result = json {| Result = "Album updated" |}
        Successful.created result next ctx

let deleteAlbumHandler (id : int) =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let albums = ctx.GetService<LiteDatabase>().GetCollection<Album>("albums")
        let result = albums.Delete(BsonValue(id))
        json result next ctx

let webApp =
    choose [
        GET >=>
            choose [
                route "/" >=> indexHandler "world"
                routef "/animals/%i" animalsHandler
                routef "/hello/%s" indexHandler
                routef "/albums/%i" getAlbumHandler
                route "/albums" >=> getAlbumListHandler ()
            ]
        PUT >=>
            choose [
                route "/albums" >=> bindJson<NewAlbum> putAlbumHandler
            ]
        POST >=>
            choose [
                routef "/albums/%i" (fun id -> bindJson<Album> (postAlbumHandler id))
            ]
        DELETE >=>
            choose [
                routef "/albums/%i" deleteAlbumHandler
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
