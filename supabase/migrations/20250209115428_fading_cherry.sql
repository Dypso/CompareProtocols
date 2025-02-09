<![CDATA[-- Tables partitionnées pour les validations
CREATE TABLE validations (
    id NUMBER GENERATED ALWAYS AS IDENTITY,
    equipment_id VARCHAR2(50),
    card_id VARCHAR2(50),
    transaction_time TIMESTAMP,
    location_id VARCHAR2(50),
    amount NUMBER(10,2),
    status VARCHAR2(20),
    creation_time TIMESTAMP DEFAULT SYSTIMESTAMP,
    CONSTRAINT pk_validations PRIMARY KEY (id, transaction_time)
)
PARTITION BY RANGE (transaction_time)
INTERVAL (NUMTODSINTERVAL(1, 'DAY'))
SUBPARTITION BY HASH (equipment_id) SUBPARTITIONS 32
(
    PARTITION p_initial VALUES LESS THAN (DATE '2024-01-01')
);

-- Tables de dimension
CREATE TABLE equipment_dim (
    equipment_id VARCHAR2(50) PRIMARY KEY,
    location_id VARCHAR2(50),
    equipment_type VARCHAR2(30),
    firmware_version VARCHAR2(20),
    last_update TIMESTAMP
);

CREATE TABLE location_dim (
    location_id VARCHAR2(50) PRIMARY KEY,
    zone_id VARCHAR2(20),
    station_name VARCHAR2(100),
    line_id VARCHAR2(20)
);

-- Index pour optimisation des requêtes
CREATE INDEX idx_val_eq_time ON validations(equipment_id, transaction_time)
LOCAL;

CREATE INDEX idx_val_card ON validations(card_id, transaction_time)
LOCAL;]]>