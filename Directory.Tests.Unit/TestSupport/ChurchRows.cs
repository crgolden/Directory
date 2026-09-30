namespace Directory.Tests.Unit.TestSupport;

using System.Data;
using Directory.Church;
using Directory.Entities;
using Directory.Enums;

internal static class ChurchRows
{
    public static DataTable Table(bool includeTotalCount)
    {
        var t = new DataTable();
        t.Columns.Add(nameof(Church.Id), typeof(Guid));
        t.Columns.Add(nameof(Church.CanonicalName), typeof(string));
        t.Columns.Add(nameof(Church.Slug), typeof(string));
        t.Columns.Add(nameof(Church.Latitude), typeof(double));
        t.Columns.Add(nameof(Church.Longitude), typeof(double));
        t.Columns.Add(nameof(Church.Street), typeof(string));
        t.Columns.Add(nameof(Church.City), typeof(string));
        t.Columns.Add(nameof(Church.State), typeof(string));
        t.Columns.Add(nameof(Church.Zip), typeof(string));
        t.Columns.Add(nameof(Church.PhoneNumber), typeof(string));
        t.Columns.Add(nameof(Church.Website), typeof(string));
        t.Columns.Add(nameof(Church.EmailAddress), typeof(string));
        t.Columns.Add(nameof(Church.DenominationId), typeof(Guid));
        t.Columns.Add(nameof(Church.WorshipStyle), typeof(int));
        t.Columns.Add(nameof(Church.PrimaryLanguage), typeof(string));
        t.Columns.Add(nameof(Church.AcceptsLGBTQ), typeof(bool));
        t.Columns.Add(nameof(Church.WheelchairAccessible), typeof(bool));
        t.Columns.Add(nameof(Church.HasNursery), typeof(bool));
        t.Columns.Add(nameof(Church.HasYouthProgram), typeof(bool));
        t.Columns.Add(nameof(Church.ConfidenceScore), typeof(decimal));
        t.Columns.Add(nameof(Church.LastVerifiedAt), typeof(DateTimeOffset));
        t.Columns.Add(nameof(Church.CreatedAt), typeof(DateTimeOffset));
        t.Columns.Add(nameof(Church.UpdatedAt), typeof(DateTimeOffset));
        t.Columns.Add(nameof(Church.IsActive), typeof(bool));
        if (includeTotalCount)
        {
            t.Columns.Add(nameof(PagedResult<Church>.TotalCount), typeof(int));
        }

        return t;
    }

    public static DataTable ActiveChurch(Guid churchId) => WithOneChurch(churchId, Generated.NewCity(), true);

    public static DataTable InactiveChurch(Guid churchId) => WithOneChurch(churchId, Generated.NewCity(), false);

    public static DataTable ActiveChurchWithBlankCity(Guid churchId) => WithOneChurch(churchId, Generated.NewBlank(), true);

    private static DataTable WithOneChurch(Guid churchId, string city, bool isActive)
    {
        var name = Generated.NewName();
        var slug = Generated.NewSlug();
        var latitude = Generated.NewLatitude();
        var longitude = Generated.NewLongitude();
        var street = Generated.NewStreet();
        var state = Generated.NewStateCodeText();
        var zip = Generated.NewZip();
        var phoneNumber = Generated.NewPhoneNumber();
        var website = Generated.NewWebsite();
        var emailAddress = Generated.NewEmailAddress();
        var denominationId = Guid.NewGuid();
        var worshipStyle = (int)Generated.NewDefinedValue<WorshipStyle>();
        var language = Generated.NewLanguage();
        var confidenceScore = Generated.NewConfidenceScore();
        var now = DateTimeOffset.UtcNow;
        var table = Table(includeTotalCount: false);
        table.Rows.Add(
            churchId,
            name,
            slug,
            latitude,
            longitude,
            street,
            city,
            state,
            zip,
            phoneNumber,
            website,
            emailAddress,
            denominationId,
            worshipStyle,
            language,
            true,
            true,
            true,
            true,
            confidenceScore,
            DBNull.Value,
            now,
            now,
            isActive);
        return table;
    }
}
