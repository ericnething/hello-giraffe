module HelloGiraffe.Types

// ---------------------------------
// Models
// ---------------------------------

type Message = {
    Text : string
}

type AnimalType =
    | Giraffe
    | Dog
    | Cat

type Animal = {
    Type : AnimalType
    FirstName : string
    LastName : string
    Age : int
    DateOfBirth : System.DateTime
}

type Genre =
    | Renaissance
    | Medieval
    | Baroque
    | Classical
    | Rock
    | Pop
    | Metal

[<CLIMutable>]
type Album = {
    Id: int
    Name: string
    DateReleased: System.DateTime
    Genre: Genre
}

[<CLIMutable>]
type NewAlbum = {
    Name: string
    Genre: Genre
}
