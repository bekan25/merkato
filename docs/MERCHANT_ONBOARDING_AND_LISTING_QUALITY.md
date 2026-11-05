# Merchant Onboarding and Product Listing Quality

## Merchant onboarding

1. A user registers with the MERCHANT role.
2. The merchant submits shop name, business type, supported city, and an Ethiopian mobile contact number.
3. MERKATO validates required fields and the mobile number format on both client and server.
4. The request is recorded as PENDING in the audit log and appears in the admin verification queue.
5. An administrator approves or rejects the submission. A rejection requires a reason.
6. A rejected merchant may correct the details and submit again. An approved merchant cannot create duplicate verification requests.
7. Fayda identity verification is separate; merchants must not submit PINs or identity document numbers in the business profile form.

Verification is a manual review workflow. It is not an automated government/company registry verification.

## Product listing quality

The merchant studio requires:
- A clear product name (3–180 characters).
- A specific category.
- A supported store city.
- A positive ETB price.
- A JPEG, PNG, or WebP product photo up to 5 MB before publishing through the web UI.

The studio displays a quality score and a checklist. Photos and complete details help buyers assess listings; the score is a guidance indicator, not a guarantee of product authenticity or seller trust.

The API validates product names, category, location, and price independently of the frontend. Image uploads remain a separate authenticated request.

## Operational boundaries

- MERKATO admin approval is an internal review, not legal certification.
- Do not describe a seller as verified until the admin approval state is recorded.
- Do not imply that a product has been independently inspected.
- Keep the existing product image upload provider configuration (Cloudinary) and test credentials in deployment before launch.
