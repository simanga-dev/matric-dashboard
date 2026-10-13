-- MatricDashboard - GeoServer/PostGIS bootstrap (sample data)
--
-- Creates the `geo` schema and a sample school-location point layer used to
-- prove the GeoServer + PostGIS + map pipeline end to end. The rows below are
-- illustrative sample points (provincial centres), NOT real school coordinates.
-- Real school coordinates are loaded in a later phase from the DBE EMIS data.
--
-- Idempotent: safe to run repeatedly against an existing database.

CREATE EXTENSION IF NOT EXISTS postgis;

CREATE SCHEMA IF NOT EXISTS geo;

CREATE TABLE IF NOT EXISTS geo.school_locations (
    id          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    emis_number text        NOT NULL,
    name        text        NOT NULL,
    province    text        NOT NULL,
    district    text,
    quintile    integer,
    wrote       integer     NOT NULL DEFAULT 0,
    passed      integer     NOT NULL DEFAULT 0,
    is_sample   boolean     NOT NULL DEFAULT true,
    geom        geometry(Point, 4326) NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_school_locations_emis
    ON geo.school_locations (emis_number);

CREATE INDEX IF NOT EXISTS ix_school_locations_geom
    ON geo.school_locations USING gist (geom);

INSERT INTO geo.school_locations
    (emis_number, name, province, district, quintile, wrote, passed, is_sample, geom)
VALUES
    ('SAMPLE-WC-001', 'Sample Secondary (Cape Town)',       'Western Cape',   'Metro Central',   4, 210, 172, true, ST_SetSRID(ST_MakePoint(18.4241, -33.9249), 4326)),
    ('SAMPLE-EC-001', 'Sample Senior Secondary (Mthatha)',  'Eastern Cape',   'OR Tambo',        2, 168, 104, true, ST_SetSRID(ST_MakePoint(28.7844, -31.5889), 4326)),
    ('SAMPLE-FS-001', 'Sample High (Bloemfontein)',         'Free State',     'Motheo',          3, 195, 141, true, ST_SetSRID(ST_MakePoint(26.1596, -29.0852), 4326)),
    ('SAMPLE-GT-001', 'Sample Secondary (Johannesburg)',    'Gauteng',        'Johannesburg East', 5, 260, 234, true, ST_SetSRID(ST_MakePoint(28.0473, -26.2041), 4326)),
    ('SAMPLE-GT-002', 'Sample High (Soweto)',               'Gauteng',        'Johannesburg West', 2, 305, 219, true, ST_SetSRID(ST_MakePoint(27.8585, -26.2678), 4326)),
    ('SAMPLE-KZ-001', 'Sample Secondary (Durban)',          'KwaZulu-Natal',  'eThekwini',       4, 288, 241, true, ST_SetSRID(ST_MakePoint(31.0218, -29.8587), 4326)),
    ('SAMPLE-KZ-002', 'Sample High (Pietermaritzburg)',     'KwaZulu-Natal',  'uMgungundlovu',   3, 176, 122, true, ST_SetSRID(ST_MakePoint(30.3794, -29.6006), 4326)),
    ('SAMPLE-LP-001', 'Sample Secondary (Polokwane)',       'Limpopo',        'Capricorn',       1, 204, 128, true, ST_SetSRID(ST_MakePoint(29.4689, -23.9045), 4326)),
    ('SAMPLE-MP-001', 'Sample High (Mbombela)',             'Mpumalanga',     'Ehlanzeni',       2, 158, 96,  true, ST_SetSRID(ST_MakePoint(30.9694, -25.4753), 4326)),
    ('SAMPLE-NC-001', 'Sample Secondary (Kimberley)',       'Northern Cape',  'Frances Baard',   3, 132, 88,  true, ST_SetSRID(ST_MakePoint(24.7499, -28.7282), 4326)),
    ('SAMPLE-NW-001', 'Sample High (Mahikeng)',             'North West',     'Ngaka Modiri Molema', 2, 149, 91, true, ST_SetSRID(ST_MakePoint(25.6445, -25.8651), 4326)),
    ('SAMPLE-NW-002', 'Sample Secondary (Rustenburg)',      'North West',     'Bojanala',        4, 223, 187, true, ST_SetSRID(ST_MakePoint(27.2424, -25.6675), 4326))
ON CONFLICT (emis_number) DO UPDATE SET
    name     = EXCLUDED.name,
    province = EXCLUDED.province,
    district = EXCLUDED.district,
    quintile = EXCLUDED.quintile,
    wrote    = EXCLUDED.wrote,
    passed   = EXCLUDED.passed,
    geom     = EXCLUDED.geom
WHERE geo.school_locations.is_sample;
