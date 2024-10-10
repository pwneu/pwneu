import requests  # type: ignore
import json
import argparse
import random
import concurrent.futures
from faker import Faker  # type: ignore

# Sample command: python seed_challenges.py --admin-password "PwneuPwneu!1" --categories-count 7 --challenges-count 20 --api-url "http://localhost:37100"

def login_admin(api_url, admin_password):
    login_payload = {
        "userName": "admin",
        "password": admin_password
    }

    headers = {'Content-Type': 'application/json'}
    response = requests.post(f"{api_url}/identity/login", data=json.dumps(login_payload), headers=headers, verify=False)

    if response.status_code == 200:
        access_token = response.json().get('accessToken')
        print("Admin logged in successfully. Access token retrieved.")
        return access_token
    else:
        print(f"Failed to log in admin. Status code: {response.status_code}, Response: {response.text}")
        return None

def create_category(api_url, access_token, category_name, category_description):
    category_payload = {
        "name": category_name,
        "description": category_description
    }

    headers = {
        'Content-Type': 'application/json',
        'Authorization': f'Bearer {access_token}'
    }

    response = requests.post(f"{api_url}/play/categories", data=json.dumps(category_payload), headers=headers, verify=False)

    if response.status_code == 200:
        category_id = response.text.strip().strip('"')
        print(f"Category created successfully: {category_id}")
        return category_id
    else:
        print(f"Failed to create category. Status code: {response.status_code}, Response: {response.text}")
        return None

def create_challenge(api_url, access_token, category_id, challenge_name, challenge_description):
    points = random.randint(1, 10) * 50

    challenge_payload = {
        "name": challenge_name,
        "description": challenge_description,
        "points": points,
        "deadlineEnabled": False,
        "deadline": "1970-01-01T00:00:00.000Z",
        "maxAttempts": 0,
        "tags": ["tag1", "tag2"],
        "flags": ["PWNEU{PWNEU}"]
    }

    headers = {
        'Content-Type': 'application/json',
        'Authorization': f'Bearer {access_token}'
    }

    response = requests.post(f"{api_url}/play/categories/{category_id}/challenges", data=json.dumps(challenge_payload), headers=headers, verify=False)

    if response.status_code == 200:
        print(f"Challenge '{challenge_name}' created successfully for category ID: {category_id}.")
    else:
        print(f"Failed to create challenge '{challenge_name}'. Status code: {response.status_code}, Response: {response.text}")

def main():
    parser = argparse.ArgumentParser(description="Create categories and challenges via API.")
    parser.add_argument("--admin-password", type=str, default="PwneuPwneu!1", help="Password for the admin user.")
    parser.add_argument("--categories-count", type=int, default=7, help="Number of categories to create.")
    parser.add_argument("--challenges-count", type=int, default=30, help="Number of challenges per category to create.")
    parser.add_argument("--api-url", type=str, default="http://localhost:37100", help="Base URL for the API.")
    args = parser.parse_args()

    api_url = args.api_url

    access_token = login_admin(api_url, args.admin_password)
    
    if access_token:
        fake = Faker()

        with concurrent.futures.ThreadPoolExecutor() as executor:
            future_to_category = {
                executor.submit(
                    create_category,
                    api_url,
                    access_token,
                    fake.word().capitalize(),
                    fake.sentence()
                ): i for i in range(args.categories_count)
            }

            for future in concurrent.futures.as_completed(future_to_category):
                category_id = future.result()
                if category_id:
                    challenge_futures = [
                        executor.submit(
                            create_challenge,
                            api_url,
                            access_token,
                            category_id,
                            fake.sentence(nb_words=3),
                            fake.sentence()
                        ) for _ in range(args.challenges_count)
                    ]

                    concurrent.futures.wait(challenge_futures)

if __name__ == "__main__":
    main()
