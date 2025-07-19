# Easy&Fast AI Question System - SJW
<img width="1000" height="112" alt="image" src="https://github.com/user-attachments/assets/d10a38cc-d9a4-4e6d-b5b0-0bb4479ef944" />


## Easy & Fast AI Question - Unity Editor Extension

"Easy & Fast AI Question" is a Unity Editor extension that allows developers to quickly and easily ask questions to AI models (Gemini and ChatGPT) directly within the Unity Editor. This tool helps streamline development by providing instant answers and insights from AI, managing question history, and offering statistical analysis of your queries.

<img width="1920" height="1080" alt="image" src="https://github.com/user-attachments/assets/790284dc-d842-47fe-92e4-4e4877f73dfe" />


# 주요 기능 (Key Features)
* AI 모델 연동 (AI Model Integration)

* Google Gemini: Gemini 1.5 Pro, Gemini 1.5 Flash, Gemini Pro Vision 모델 지원.

* OpenAI ChatGPT: gpt-3.5-turbo, gpt-4o, gpt-4-turbo 모델 지원.

* API 키 관리 (API Key Management): 각 AI 서비스별 API 키를 안전하게 저장하고 관리할 수 있습니다.

* 질문 및 답변 기록 (Question & Answer History):

* 질문과 AI 답변 내역을 저장하고 열람할 수 있습니다.

* 질문을 중요 표시하거나 검색 및 필터링할 수 있습니다.

* 질문/답변 텍스트 색상을 커스터마이징할 수 있습니다.

* 각 질문에 대한 메모를 추가하고 관리할 수 있습니다.



# 통계 분석 (Statistics Analysis):
* 질문 내역을 기반으로 키워드 사용 빈도를 날짜별로 분석합니다.
* AI를 활용하여 질문 내역 전체를 분석하고, 그 결과를 기록으로 남길 수 있습니다.
* 직관적인 UI (Intuitive UI): Unity Editor 내에 통합된 탭 기반 인터페이스로 쉽게 기능에 접근할 수 있습니다.



# 사용 방법 (How to Use)
## 1. 설치 (Installation)
* 이 프로젝트의 모든 C# 파일을 Unity 프로젝트의 Assets/Editor 폴더 (또는 Editor 스크립트가 인식되는 다른 폴더)에 추가합니다.

## 2. 툴 열기 (Opening the Tool)
* Unity Editor 상단 메뉴에서 Tools(도구) > Easy & Fast AI Question 을 클릭하여 툴 창을 엽니다.

## 3. API 키 설정 (Setting Up API Keys)
* 툴 창에서 "Gemini" 또는 "ChatGPT" 탭을 선택합니다.
* 해당 AI 서비스의 API 키를 입력하고 "승인" 버튼을 클릭하여 저장합니다.
* Gemini API 키는 Google AI Studio에서 발급받을 수 있습니다.
* ChatGPT (OpenAI) API 키는 OpenAI Platform에서 발급받을 수 있습니다.
* 저장된 API 키는 Unity의 EditorPrefs에 저장되며, 언제든지 "수정" 또는 "API 키 초기화" 할 수 있습니다.

## 4. AI에게 질문하기 (Asking Questions to AI)
* "Gemini" 또는 "ChatGPT" 탭에서 원하는 AI 모델을 선택합니다.
* 하단의 입력 필드에 질문을 입력하고 "질문하기" 버튼을 클릭합니다.
* AI의 답변이 채팅 기록에 표시됩니다.

## 5. 질문 리스트 활용 (Using the Question List)
* "질문 리스트" 탭에서 저장된 모든 질문 기록을 볼 수 있습니다.
* 상단의 검색 필드와 카테고리 탭 (전체, 중요, Gemini, ChatGPT)을 사용하여 질문을 필터링하고 검색할 수 있습니다.
* 각 질문 항목의 "상세 보기" 버튼을 클릭하면 질문 내용, 답변, 메모, 시간 기록 등을 확인할 수 있는 상세 창이 열립니다.
* 상세 창에서 해당 질문에 대한 메모를 추가하거나, 질문을 삭제할 수 있습니다.
* "텍스트 색상 설정" 섹션에서 질문과 답변 텍스트의 색상을 변경할 수 있습니다.

## 6. 통계 분석 확인 (Checking Statistics Analysis)
* "통계 분석" 탭에서 질문 내역에 대한 다양한 통계를 확인할 수 있습니다.
* "기간별 키워드 사용 빈도" 섹션에서 질문에 사용된 키워드의 통계를 볼 수 있습니다.
* "AI로 질문 내역 분석" 섹션에서 Gemini 또는 ChatGPT를 사용하여 전체 질문 내역에 대한 AI 분석을 수행할 수 있습니다. 분석 결과는 **"AI 분석 히스토리"**에 기록됩니다.

---

## 📄 라이선스

이 소프트웨어는 명시적 또는 묵시적인 어떠한 종류의 보증도 없이 "있는 그대로" 제공됩니다. 상품성, 특정 목적에의 적합성 및 비침해에 대한 보증을 포함하되 이에 국한되지 않습니다. 어떠한 경우에도 작성자나 저작권 보유자는 소프트웨어 또는 소프트웨어의 사용 또는 기타 거래와 관련하여 발생하는 모든 청구, 손해 또는 기타 책임에 대해 계약, 불법 행위 또는 기타 방식으로 책임을 지지 않습니다.

**Copyright (c) Sonjongwook**

문의 사항이나 문제가 있는 경우 개발자에게 문의하십시오.
