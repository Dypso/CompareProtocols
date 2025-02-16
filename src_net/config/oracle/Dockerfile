FROM gvenzl/oracle-free:latest

# Copy initialization scripts
COPY init.sql /docker-entrypoint-initdb.d/

# Set environment variables for optimal performance
ENV ORACLE_ENABLE_XDB=true \
    ORACLE_ALLOW_REMOTE=true \
    ORACLE_DISABLE_ASYNC_IO=false \
    ORACLE_CPU_COUNT=2 \
    USE_SID_AS_SERVICE_listener=on

# Create necessary directories and modify listener configuration
RUN mkdir -p /opt/oracle/network/admin \
    && echo "LISTENER = \n\
  (DESCRIPTION_LIST = \n\
    (DESCRIPTION = \n\
      (ADDRESS = (PROTOCOL = TCP)(HOST = 0.0.0.0)(PORT = 1521)) \n\
      (PROTOCOL_STACK = \n\
        (PRESENTATION = TTC) \n\
        (SESSION = NS)) \n\
    ) \n\
  ) \n\
\n\
SID_LIST_LISTENER = \n\
  (SID_LIST = \n\
    (SID_DESC = \n\
      (SID_NAME = BILLETDB) \n\
      (ORACLE_HOME = /opt/oracle) \n\
      (GLOBAL_DBNAME = BILLETDB) \n\
    ) \n\
  )" > /opt/oracle/network/admin/listener.ora

# Create directory for custom scripts and set permissions
RUN mkdir -p /opt/oracle/scripts/startup \
    && chown -R oracle:oinstall /opt/oracle/scripts \
    && chown -R oracle:oinstall /opt/oracle/network