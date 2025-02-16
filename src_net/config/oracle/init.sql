-- Create tablespace for validation data
CREATE TABLESPACE validation_data
    DATAFILE 'validation_data01.dbf'
    SIZE 1G
    AUTOEXTEND ON
    NEXT 500M
    MAXSIZE 10G;

-- Create sequence for validation events
CREATE SEQUENCE validation_event_seq
    START WITH 1
    INCREMENT BY 1
    CACHE 100
    NOCYCLE;

-- Create validation events table with partitioning
CREATE TABLE validation_events (
    id NUMBER DEFAULT validation_event_seq.NEXTVAL PRIMARY KEY,
    tenant_id VARCHAR2(50) NOT NULL,
    token_id VARCHAR2(100) NOT NULL,
    validation_type VARCHAR2(20) NOT NULL,
    equipment_id VARCHAR2(50) NOT NULL,
    status VARCHAR2(20) NOT NULL,
    validation_time TIMESTAMP NOT NULL,
    location_id VARCHAR2(50),
    metadata CLOB,
    created_at TIMESTAMP DEFAULT SYSTIMESTAMP NOT NULL
)
PARTITION BY RANGE (validation_time)
INTERVAL (NUMTODSINTERVAL(1, 'DAY'))
(PARTITION validation_events_p1 VALUES LESS THAN (TIMESTAMP '2025-01-01 00:00:00'));

-- Create indexes for efficient querying
CREATE INDEX idx_validation_tenant ON validation_events(tenant_id, validation_time);
CREATE INDEX idx_validation_token ON validation_events(token_id, validation_time);
CREATE INDEX idx_validation_equipment ON validation_events(equipment_id, validation_time);
CREATE INDEX idx_validation_status ON validation_events(status, validation_time);

-- Create materialized view for analytics
CREATE MATERIALIZED VIEW validation_stats
    BUILD IMMEDIATE
    REFRESH FAST ON COMMIT
    ENABLE QUERY REWRITE
AS
SELECT 
    tenant_id,
    validation_type,
    status,
    TRUNC(validation_time, 'HH') as time_window,
    COUNT(*) as validation_count,
    COUNT(DISTINCT token_id) as unique_tokens,
    COUNT(DISTINCT equipment_id) as unique_equipment
FROM validation_events
GROUP BY 
    tenant_id,
    validation_type,
    status,
    TRUNC(validation_time, 'HH');

-- Create indexes on materialized view
CREATE INDEX idx_validation_stats_tenant ON validation_stats(tenant_id, time_window);
CREATE INDEX idx_validation_stats_type ON validation_stats(validation_type, time_window);

-- Grant privileges to application user
GRANT SELECT, INSERT, UPDATE ON validation_events TO billettique;
GRANT SELECT ON validation_stats TO billettique;
GRANT SELECT ON validation_event_seq TO billettique;

-- Create tablespace for temporary tables
CREATE TEMPORARY TABLESPACE validation_temp
    TEMPFILE 'validation_temp01.dbf'
    SIZE 500M
    AUTOEXTEND ON
    NEXT 100M
    MAXSIZE 2G;