namespace Directory.Campuses;

public record CampusRequest(
    string Name,
    string? Street,
    string City,
    string State,
    string Zip,
    double Latitude,
    double Longitude);
