from app.mongodb.db import get_db
from app.setting import MONGO_URI
from pymongo import MongoClient


def ping() -> bool:
    try:
        client = MongoClient(MONGO_URI, timeoutMS = 5000)
        client.admin.command("ping")
        return True
    except:
        return False
