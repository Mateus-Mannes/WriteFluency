using Bogus;

namespace WriteFluency.Propositions;

public static class NewsDtoFaker
{
    public static IEnumerable<NewsDto> Generate(
        int quantity = 1,
        SubjectEnum? subject = null,
        DateTime? publishedBefore = null)
    {
        var faker = new Faker();
        for (int i = 0; i < quantity; i++)
        {
            var publishedOn = publishedBefore.HasValue
                ? publishedBefore.Value.AddMinutes(-(i + 1))
                : faker.Date.Past(1, DateTime.UtcNow);

            yield return new NewsDto(
                faker.Random.Guid().ToString(),
                faker.Lorem.Sentence(),
                faker.Lorem.Paragraph(),
                faker.Internet.Url(),
                faker.Internet.Url(),
                subject ?? faker.PickRandom<SubjectEnum>(),
                publishedOn
            );
        }
    }
}
