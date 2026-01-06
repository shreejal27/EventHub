using EventHub.Domain.Exceptions;

namespace EventHub.Domain.ValueObjects
{
    public class Location
    {
        public string Address { get; private set; }

        public string City { get; private set; }

        public string Country { get; private set; }

        public double? Latitude { get; private set; }

        public double? Longitude { get; private set; }

        private Location()
        {
            Address = string.Empty;
            City = string.Empty;
            Country = string.Empty;
        }

        public Location(
            string address,
            string city,
            string country,
            double? latitude = null,
            double? longitude = null)
        {

            if (string.IsNullOrWhiteSpace(address))
                throw new DomainException("Location address cannot be empty");

            if (string.IsNullOrWhiteSpace(city))
                throw new DomainException("Location city cannot be empty");

            if (string.IsNullOrWhiteSpace(country))
                throw new DomainException("Location country cannot be empty");

            if (latitude.HasValue && (latitude < -90 || latitude > 90))
                throw new DomainException("Latitude must be between -90 and 90 degrees");

            if (longitude.HasValue && (longitude < -180 || longitude > 180))
                throw new DomainException("Longitude must be between -180 and 180 degrees");

            if (latitude.HasValue != longitude.HasValue)
                throw new DomainException("Both latitude and longitude must be provided together");

            Address = address.Trim();
            City = city.Trim();
            Country = country.Trim();
            Latitude = latitude;
            Longitude = longitude;
        }

        public override string ToString()
        {
            return $"{Address}, {City}, {Country}";
        }

        public bool HasCoordinates()
        {
            return Latitude.HasValue && Longitude.HasValue;
        }
    }
}
