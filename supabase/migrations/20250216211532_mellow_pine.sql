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

-- Create user and grant privileges
CREATE USER billettique IDENTIFIED BY billettique123!
    DEFAULT TABLESPACE validation_data
    TEMPORARY TABLESPACE temp
    QUOTA UNLIMITED ON validation_data;

-- Grant necessary privileges
GRANT CREATE SESSION TO billettique;
GRANT SELECT, INSERT, UPDATE ON validation_events TO billettique;
GRANT SELECT ON validation_event_seq TO billettique;

-- Create tablespace for temporary tables
CREATE TEMPORARY TABLESPACE validation_temp
    TEMPFILE 'validation_temp01.dbf'
    SIZE 500M
    AUTOEXTEND ON
    NEXT 100M
    MAXSIZE 2G;