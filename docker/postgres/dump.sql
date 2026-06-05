--
-- PostgreSQL database dump
--

\restrict vq55elh7TpdqtkJen57Rbr005t6x7gfQGfLhWFqJjVxnU6haYkrGI9amWfIpcBL

-- Dumped from database version 18.3
-- Dumped by pg_dump version 18.3

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: __EFMigrationsHistory; Type: TABLE; Schema: public; Owner: progcomp_user
--

CREATE TABLE public."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL
);


ALTER TABLE public."__EFMigrationsHistory" OWNER TO progcomp_user;

--
-- Name: organizations; Type: TABLE; Schema: public; Owner: progcomp_user
--

CREATE TABLE public.organizations (
    "Id" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "ShortName" character varying(50) NOT NULL,
    "LogoUrl" text
);


ALTER TABLE public.organizations OWNER TO progcomp_user;

--
-- Name: refresh_tokens; Type: TABLE; Schema: public; Owner: progcomp_user
--

CREATE TABLE public.refresh_tokens (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "TokenHash" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "ExpiresAtUtc" timestamp with time zone NOT NULL,
    "RevokedAtUtc" timestamp with time zone
);


ALTER TABLE public.refresh_tokens OWNER TO progcomp_user;

--
-- Name: user_roles; Type: TABLE; Schema: public; Owner: progcomp_user
--

CREATE TABLE public.user_roles (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "RoleName" character varying(50) NOT NULL
);


ALTER TABLE public.user_roles OWNER TO progcomp_user;

--
-- Name: users; Type: TABLE; Schema: public; Owner: progcomp_user
--

CREATE TABLE public.users (
    "Id" uuid NOT NULL,
    "Nickname" character varying(50) NOT NULL,
    "Email" text NOT NULL,
    "Names" character varying(50) NOT NULL,
    "Surnames" character varying(50) NOT NULL,
    "DateOfBirth" date,
    "OrganizationId" uuid,
    "FemTeamEligible" boolean NOT NULL,
    "CodeforcesHandle" text,
    "CodeforcesRating" integer NOT NULL,
    "AtcoderHandle" text,
    "AtcoderRating" integer NOT NULL,
    "CsesHandle" text,
    "CsesRating" integer NOT NULL,
    "LeetCodeHandle" text,
    "LeetCodeRating" integer NOT NULL,
    "CodeChefHandle" text,
    "CodeChefRating" integer NOT NULL,
    "LuoguHandle" text,
    "LuoguRating" integer NOT NULL,
    "PasswordHash" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL
);


ALTER TABLE public.users OWNER TO progcomp_user;

--
-- Data for Name: __EFMigrationsHistory; Type: TABLE DATA; Schema: public; Owner: progcomp_user
--

COPY public."__EFMigrationsHistory" ("MigrationId", "ProductVersion") FROM stdin;
20260322222435_InitialCreate	10.0.5
\.


--
-- Data for Name: organizations; Type: TABLE DATA; Schema: public; Owner: progcomp_user
--

COPY public.organizations ("Id", "Name", "ShortName", "LogoUrl") FROM stdin;
b46bb6e2-6ff0-4919-93b1-cc99a55931b0	Universidad de Chile	UChile	\N
5b0751af-9ef1-446b-a473-5f7b408703ec	Universidad Tecnica Federico Santa Maria	UTFSM	utfsm
6fa2326a-2aad-496d-bffd-b9a57d66137d	Pontificia Universidad Catolica de Chile	PUC	puc
\.


--
-- Data for Name: refresh_tokens; Type: TABLE DATA; Schema: public; Owner: progcomp_user
--

COPY public.refresh_tokens ("Id", "UserId", "TokenHash", "CreatedAtUtc", "ExpiresAtUtc", "RevokedAtUtc") FROM stdin;
8caefce7-b7de-4fcd-9b88-0402ae9f902a	f528d0b4-edbd-47bd-b762-de82fbb799c7	0402CABC72C48EA01E1899E27ABD24EC8B22F3B68F0846FD5050B628429591ED	2026-03-22 20:35:43.905845-03	2026-04-21 19:35:43.906207-04	\N
91028b40-f229-4106-aee9-129397b1470c	f528d0b4-edbd-47bd-b762-de82fbb799c7	60DF08111B6CACC3DE7E0810E0B1D864E79DB3F5E0B16CFF3042D2DC4867AF14	2026-03-22 21:40:17.19033-03	2026-04-21 20:40:17.190331-04	\N
100bd4fe-92df-4686-9401-0008003cc58a	f528d0b4-edbd-47bd-b762-de82fbb799c7	C25EF85A9369EC4D1EA378A2A5C57CBEBD080BA9B73F8D2758DC46C08F832A74	2026-03-22 21:42:09.371162-03	2026-04-21 20:42:09.371162-04	\N
6f0207b5-a7ad-4b88-8b97-3954a9bc2578	f528d0b4-edbd-47bd-b762-de82fbb799c7	36547BE17C009CA5E683C5458207C4BAC533C9120D1E25082EE2DD0833C8CDF6	2026-03-22 21:45:40.364284-03	2026-04-21 20:45:40.364284-04	\N
912b1c49-590e-4cf9-9093-bb707ab7199d	f528d0b4-edbd-47bd-b762-de82fbb799c7	2020E6D7311DC355F5814B8A8329EABCD9995C063227F026E4A45F91B45289BA	2026-03-22 21:58:33.734519-03	2026-04-21 20:58:33.73487-04	\N
2983b0ef-413c-4a14-b3d4-53c9047bf5d7	f528d0b4-edbd-47bd-b762-de82fbb799c7	C50559D595CEE8C559E68A7EA4331A5215F7EDFDEE4F711543946A4524F8A80A	2026-03-22 21:58:41.88346-03	2026-04-21 20:58:41.883461-04	\N
3501da6d-5640-40c0-a8f2-21fdc8c02e0f	f528d0b4-edbd-47bd-b762-de82fbb799c7	B47F854662A9C4C91490452BF0CA3AEF898D3933C4E14A9327B9128839044F35	2026-03-22 22:23:26.089812-03	2026-04-21 21:23:26.090069-04	\N
57070b42-c6ba-4c43-b941-8ad31f68b6d1	f528d0b4-edbd-47bd-b762-de82fbb799c7	9427062CFF0E170B2CD17962D633E994B0109A9AC8F3480A3BB08F3F611A2E09	2026-03-22 23:12:06.237454-03	2036-03-22 23:12:06.237702-03	\N
6a64b8d8-795d-4b91-9062-a6ec07f00ef0	4fb9ba3c-ab38-492a-ba47-a3b181960add	12FC16ABDA0302037CCD6CDFB35847FEAE1689864907F86EAFAE4D400A00FC19	2026-03-22 23:36:32.606264-03	2036-03-22 23:36:32.606264-03	\N
1c583bac-329b-474e-8b58-e37ae78e9a03	f528d0b4-edbd-47bd-b762-de82fbb799c7	325DD497AE7FB701187ACA6646503AEB44534885743C45B3CC3688668626E460	2026-03-22 23:59:51.666807-03	2026-03-23 23:59:51.666808-03	\N
7ff538fd-2f4e-4a42-bdf0-e7ddd73802da	f528d0b4-edbd-47bd-b762-de82fbb799c7	EB1A3F8FA95F98D537F5639B431145B2485973AFE65E7F8162029FEE1E2F83C6	2026-03-23 21:18:17.820921-03	2026-03-24 21:18:17.821454-03	\N
\.


--
-- Data for Name: user_roles; Type: TABLE DATA; Schema: public; Owner: progcomp_user
--

COPY public.user_roles ("Id", "UserId", "RoleName") FROM stdin;
2b387f04-7982-4562-9537-dd1bcfba6d40	f528d0b4-edbd-47bd-b762-de82fbb799c7	Admin
cc938d24-7e7e-4707-baeb-4088fe7564ac	4fb9ba3c-ab38-492a-ba47-a3b181960add	User
3ef575d0-7553-43fd-bd21-499d57f2c0ad	8466c1f9-8243-40c6-a16a-84bcde35b914	User
7078a2b5-b5c3-4654-a1dd-e3d22612b3eb	b097f4f5-afc6-4507-be4d-3ad3a223f687	User
3bffd088-c9fb-4aa5-a1c1-9935087c2bea	41dec8d3-fe63-4c51-9c28-da569325f60e	User
58a5bab1-1872-494d-96cc-f79c5c9c2e11	23735d27-82df-4d0d-8d77-98779cb51e77	User
\.


--
-- Data for Name: users; Type: TABLE DATA; Schema: public; Owner: progcomp_user
--

COPY public.users ("Id", "Nickname", "Email", "Names", "Surnames", "DateOfBirth", "OrganizationId", "FemTeamEligible", "CodeforcesHandle", "CodeforcesRating", "AtcoderHandle", "AtcoderRating", "CsesHandle", "CsesRating", "LeetCodeHandle", "LeetCodeRating", "CodeChefHandle", "CodeChefRating", "LuoguHandle", "LuoguRating", "PasswordHash", "IsActive", "CreatedAtUtc", "UpdatedAtUtc") FROM stdin;
4fb9ba3c-ab38-492a-ba47-a3b181960add	MrYhatoh	gabriel.carmona@outlook.com	Gabriel	Carmona	1998-01-04	b46bb6e2-6ff0-4919-93b1-cc99a55931b0	f	MrYhatoh	1409	\N	0	\N	0	\N	0	\N	0	\N	0	AQAAAAIAAYagAAAAEC8g2W5Fz4bPfiVeAn5NxFY7H7Tk4C76ZzaXIgHTX3okTsW2SmTjRrye4ANJao5RLQ==	t	2026-03-22 22:25:06.606866-03	2026-03-22 22:25:06.607038-03
b097f4f5-afc6-4507-be4d-3ad3a223f687	Diego	Diego@gmail.com	Diego	Arias	2001-01-01	b46bb6e2-6ff0-4919-93b1-cc99a55931b0	f	dariasc	1780	\N	0	\N	0	\N	0	\N	0	\N	0	AQAAAAIAAYagAAAAECsPkLWc+F5/YEP1fEzfuyqnzUq5Rfg25nvJpSa7WSgNrbU0eWXm9Pm40wS9UZoLHA==	t	2026-03-23 21:20:57.529794-03	2026-03-23 21:20:57.529794-03
23735d27-82df-4d0d-8d77-98779cb51e77	Marceantasy	s	Marcelo	Lemus	2003-01-01	6fa2326a-2aad-496d-bffd-b9a57d66137d	f	Marceantasy	2138	\N	0	\N	0	\N	0	\N	0	\N	0	AQAAAAIAAYagAAAAEOglJ7zLE1SKrJz78qmKOtDkr0f2yM9S6kCi/p7higFac55k/uS04a6XA/DgcpZ1XA==	t	2026-03-23 21:24:17.488298-03	2026-03-23 21:24:17.488298-03
41dec8d3-fe63-4c51-9c28-da569325f60e	abner.vidal	asd	Abner	Vidal	2003-01-01	5b0751af-9ef1-446b-a473-5f7b408703ec	f	abner_vidal	1783	\N	0	\N	0	\N	0	\N	0	\N	0	AQAAAAIAAYagAAAAEIOZ7W+J9MN9jx1cPq0WavR77fD/+bhdKy2QSmA2SnoxcNC/aJ2zJoDeDw4zeNfaRg==	t	2026-03-23 21:23:48.222965-03	2026-03-23 21:23:48.222966-03
8466c1f9-8243-40c6-a16a-84bcde35b914	Dmitri	Dmitri@gmail.com	Dmitri	Ramirez	1999-05-01	b46bb6e2-6ff0-4919-93b1-cc99a55931b0	f	svandich	1107	\N	0	\N	0	\N	0	\N	0	\N	0	AQAAAAIAAYagAAAAEBFaEOUJMyA9+XY0OAlpV63YPGi5JLG9Esf/dHnKVPUVCKK6YDXGl1Ewqz5u1EkZKQ==	t	2026-03-23 21:19:00.328411-03	2026-03-23 21:19:00.328507-03
f528d0b4-edbd-47bd-b762-de82fbb799c7	JOliva	jav.oliva.silva	Javier	Oliva	\N	\N	f	JOliva	2164	JOliva	2031	JOliva	0	JavOliva	0	javolivasilva	0	JOliva	0	AQAAAAIAAYagAAAAEDgDwPo7wUppX3Rto3vXKu0WO05OojtOA0xIa44IBRSDo0q1QgGe4iBZFghlrRI2Pg==	t	-infinity	-infinity
\.


--
-- Name: __EFMigrationsHistory PK___EFMigrationsHistory; Type: CONSTRAINT; Schema: public; Owner: progcomp_user
--

ALTER TABLE ONLY public."__EFMigrationsHistory"
    ADD CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId");


--
-- Name: organizations PK_organizations; Type: CONSTRAINT; Schema: public; Owner: progcomp_user
--

ALTER TABLE ONLY public.organizations
    ADD CONSTRAINT "PK_organizations" PRIMARY KEY ("Id");


--
-- Name: refresh_tokens PK_refresh_tokens; Type: CONSTRAINT; Schema: public; Owner: progcomp_user
--

ALTER TABLE ONLY public.refresh_tokens
    ADD CONSTRAINT "PK_refresh_tokens" PRIMARY KEY ("Id");


--
-- Name: user_roles PK_user_roles; Type: CONSTRAINT; Schema: public; Owner: progcomp_user
--

ALTER TABLE ONLY public.user_roles
    ADD CONSTRAINT "PK_user_roles" PRIMARY KEY ("Id");


--
-- Name: users PK_users; Type: CONSTRAINT; Schema: public; Owner: progcomp_user
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT "PK_users" PRIMARY KEY ("Id");


--
-- Name: IX_organizations_Name; Type: INDEX; Schema: public; Owner: progcomp_user
--

CREATE UNIQUE INDEX "IX_organizations_Name" ON public.organizations USING btree ("Name");


--
-- Name: IX_organizations_ShortName; Type: INDEX; Schema: public; Owner: progcomp_user
--

CREATE UNIQUE INDEX "IX_organizations_ShortName" ON public.organizations USING btree ("ShortName");


--
-- Name: IX_refresh_tokens_TokenHash; Type: INDEX; Schema: public; Owner: progcomp_user
--

CREATE UNIQUE INDEX "IX_refresh_tokens_TokenHash" ON public.refresh_tokens USING btree ("TokenHash");


--
-- Name: IX_refresh_tokens_UserId; Type: INDEX; Schema: public; Owner: progcomp_user
--

CREATE INDEX "IX_refresh_tokens_UserId" ON public.refresh_tokens USING btree ("UserId");


--
-- Name: IX_user_roles_UserId_RoleName; Type: INDEX; Schema: public; Owner: progcomp_user
--

CREATE UNIQUE INDEX "IX_user_roles_UserId_RoleName" ON public.user_roles USING btree ("UserId", "RoleName");


--
-- Name: IX_users_Nickname; Type: INDEX; Schema: public; Owner: progcomp_user
--

CREATE UNIQUE INDEX "IX_users_Nickname" ON public.users USING btree ("Nickname");


--
-- Name: IX_users_OrganizationId; Type: INDEX; Schema: public; Owner: progcomp_user
--

CREATE INDEX "IX_users_OrganizationId" ON public.users USING btree ("OrganizationId");


--
-- Name: refresh_tokens FK_refresh_tokens_users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: progcomp_user
--

ALTER TABLE ONLY public.refresh_tokens
    ADD CONSTRAINT "FK_refresh_tokens_users_UserId" FOREIGN KEY ("UserId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: user_roles FK_user_roles_users_UserId; Type: FK CONSTRAINT; Schema: public; Owner: progcomp_user
--

ALTER TABLE ONLY public.user_roles
    ADD CONSTRAINT "FK_user_roles_users_UserId" FOREIGN KEY ("UserId") REFERENCES public.users("Id") ON DELETE CASCADE;


--
-- Name: users FK_users_organizations_OrganizationId; Type: FK CONSTRAINT; Schema: public; Owner: progcomp_user
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT "FK_users_organizations_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES public.organizations("Id") ON DELETE SET NULL;


--
-- PostgreSQL database dump complete
--

\unrestrict vq55elh7TpdqtkJen57Rbr005t6x7gfQGfLhWFqJjVxnU6haYkrGI9amWfIpcBL

