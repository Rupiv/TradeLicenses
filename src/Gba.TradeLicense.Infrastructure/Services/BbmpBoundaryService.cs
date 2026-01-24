using System;
using System.IO;
using System.Linq;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace Gba.TradeLicense.Infrastructure.Services
{
    public class BbmpBoundaryService
    {
        private readonly Geometry _bbmpGeometry;
        private readonly MathTransform _wgs84ToUtm43;

        public BbmpBoundaryService()
        {
            // ---------------- Load GeoJSON (EPSG:32643) ----------------
            var geoJsonPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "geojson",
                "bbmp_boundary.geojson"
            );

            var geoJson = File.ReadAllText(geoJsonPath);
            var reader = new GeoJsonReader();
            var featureCollection = reader.Read<FeatureCollection>(geoJson);

            // ---------------- UNION ALL POLYGONS ----------------
            var geometries = featureCollection
                .Select(f => f.Geometry)
                .ToArray();

            _bbmpGeometry = GeometryFactory.Default
                .BuildGeometry(geometries)
                .Union();

            _bbmpGeometry.SRID = 32643;

            // ---------------- CRS TRANSFORMATION ----------------
            var csFactory = new CoordinateSystemFactory();
            var ctFactory = new CoordinateTransformationFactory();

            var wgs84 = GeographicCoordinateSystem.WGS84;          // EPSG:4326
            var utm43 = ProjectedCoordinateSystem.WGS84_UTM(43, true); // EPSG:32643

            _wgs84ToUtm43 = ctFactory
                .CreateFromCoordinateSystems(wgs84, utm43)
                .MathTransform;
        }

        public bool IsInsideBbmp(decimal latitude, decimal longitude)
        {
            // Convert WGS84 → UTM Zone 43N
            var utm = _wgs84ToUtm43.Transform(
                new[] { (double)longitude, (double)latitude }
            );

            var point = new Point(utm[0], utm[1])
            {
                SRID = 32643
            };

            // Covers = inside + boundary (BEST for authority limits)
            return _bbmpGeometry.Covers(point);
        }
    }
}
