module HelloGiraffe.Types

// ---------------------------------
// Models
// ---------------------------------

type Message = {
    Text : string
}

type AnimalType =
    | AnimalGiraffe
    | AnimalDog
    | AnimalCat

type Animal = {
    Type : AnimalType
    FirstName : string
    LastName : string
    Age : int
    DateOfBirth : System.DateTime
}

type Genre = Rock | Pop | Metal

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

let toAlbum ({
    NewAlbum.Name = name
    Genre = genre
} : NewAlbum) : Album = {
    Id = 0
    Name = name
    DateReleased = System.DateTime.Now
    Genre = genre
}
