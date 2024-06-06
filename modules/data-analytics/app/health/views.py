from app.health import get_health_blueprint
from flask import current_app, jsonify
from utils import internal_error_handler
from app.mongodb.commands import ping as pingmongo


OK = 200
INTERNAL_SERVER_ERROR = 500
SERVICE_UNAVAILABLE = 503

health = get_health_blueprint()
health.register_error_handler(INTERNAL_SERVER_ERROR, internal_error_handler)

def get_health_response(status_code, status_text, error = ''):
    return jsonify(code=status_code, error=error, data={'state': f'{current_app.config["ENV"]} {status_text}'}), status_code

def unhealthy(reason):
    return get_health_response(SERVICE_UNAVAILABLE, "Unavailable", reason)

def healthy():
    return get_health_response(OK, "OK", "")

@health.route('/liveness', methods=['GET'])
def get_liveness():
    return healthy()

@health.route('/readiness', methods=['GET'])
def get_readiness():
    if not pingmongo():
        print("Mongo not available")
        return unhealthy("The MongoDB database is currently unavailable.")
    elif not True:
        return unhealthy("Redis is currently unavailable.")
    elif not True:
        return unhealthy("Kafka is currently unavailable.")
    elif not True:
        return unhealthy("Clickhouse is currently unavailable.")
    
    return healthy()