CREATE FUNCTION [dbo].[fn_HaversineDistance]
(
    @lat1 FLOAT,
    @lon1 FLOAT,
    @lat2 FLOAT,
    @lon2 FLOAT
)
RETURNS FLOAT
AS
BEGIN
    DECLARE @R    FLOAT = 3958.8;
    DECLARE @dLat FLOAT = RADIANS(@lat2 - @lat1);
    DECLARE @dLon FLOAT = RADIANS(@lon2 - @lon1);
    DECLARE @a    FLOAT = SIN(@dLat / 2) * SIN(@dLat / 2)
                        + COS(RADIANS(@lat1)) * COS(RADIANS(@lat2))
                        * SIN(@dLon / 2) * SIN(@dLon / 2);
    RETURN @R * 2 * ATN2(SQRT(@a), SQRT(1 - @a));
END
