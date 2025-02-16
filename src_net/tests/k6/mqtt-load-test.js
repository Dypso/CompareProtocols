import mqtt from 'k6/x/mqtt';
import { check, sleep } from 'k6';
import { Counter, Trend } from 'k6/metrics';
import exec from 'k6/execution';

const validationsSent = new Counter('validations_sent');
const validationLatency = new Trend('validation_latency');
const connectionLatency = new Trend('connection_latency');

export const options = {
    scenarios: {
        constant_load: {
            executor: 'constant-vus',
            vus: 50,
            duration: '5m'
        },
        ramp_up: {
            executor: 'ramping-vus',
            startVUs: 0,
            stages: [
                { duration: '2m', target: 100 },
                { duration: '5m', target: 100 },
                { duration: '2m', target: 0 }
            ]
        }
    },
    thresholds: {
        'validation_latency': ['p(95)<500'],
        'connection_latency': ['p(95)<1000']
    }
};

const brokerConfig = {
    broker: 'ssl://localhost:8883',
    username: 'admin',
    password: 'admin123!',
    clientId: 'k6-mqtt-',
    clean: true,
};

function createValidationMessage() {
    return JSON.stringify({
        equipmentId: `EQ-${exec.vu.idInTest}-${exec.vu.iterationInScenario}`,
        tokenId: `TOKEN-${Math.random().toString(36)}`,
        timestamp: new Date().toISOString(),
        location: 'STATION-1',
        amount: Math.random() * 100,
        type: 'ENTRY',
        status: 'PENDING',
        sequence: exec.vu.iterationInScenario,
        sessionId: `SESSION-${exec.vu.idInTest}`,
        metadata: {
            deviceType: 'VALIDATOR',
            firmwareVersion: '1.0.0',
            lineId: 'LINE-1'
        }
    });
}

export default function () {
    const client = new mqtt.Client(brokerConfig);
    
    const startConnTime = new Date();
    client.connect();
    connectionLatency.add(new Date() - startConnTime);

    check(client, {
        'connected to broker': () => client.isConnected(),
    });

    if (!client.isConnected()) {
        console.error('Failed to connect to MQTT broker');
        return;
    }

    // Subscribe to response topic
    const responseTopic = `responses/${exec.vu.idInTest}/#`;
    client.subscribe(responseTopic);

    // Set up response handling
    client.on('message', (topic, message) => {
        const response = JSON.parse(message);
        check(response, {
            'response has success status': (r) => r.success === true,
            'response has valid timestamp': (r) => new Date(r.processedAt).getTime() > 0
        });
    });

    // Send validation messages
    for (let i = 0; i < 10; i++) {
        const startTime = new Date();
        const message = createValidationMessage();
        const topic = `validations/${exec.vu.idInTest}/${i}`;
        
        client.publish(topic, message, 2); // QoS 2
        validationLatency.add(new Date() - startTime);
        validationsSent.add(1);

        sleep(0.1); // Small delay between messages
    }

    sleep(1); // Wait for responses

    client.disconnect();
    check(client, {
        'disconnected from broker': () => !client.isConnected(),
    });
}

export function teardown() {
    // Cleanup if needed
}